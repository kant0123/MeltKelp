// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

using System.Diagnostics;

namespace Meltype.Composition;

/// <summary>
/// MeltKelp: 変換の直前に、キャレットの前の確定済みの文字列を受け取る変換エンジン (MeltKelp #2)。
/// 変換エンジン (HybridConverter) は変換ボックスの CompositionController と Meltype IME の入力欄ごとの MeltypeSession で共有されるので、
/// 特定の Controller から読むのではなく、変換を頼む側がそのつど渡す (CompositionController.ConvertJapanese の先頭)。
/// </summary>
public interface IPrecedingTextReceiver
{
    /// <summary>これから変換するかなの前にある、確定済みの文字列 (取れなければ null)。上流の文脈 (同じ文の日本語・最大 10 文字) とは別のもの。</summary>
    void SetPrecedingText(string? text);
}

/// <summary>MeltKelp: 変換候補を言語モデルで採点する (zenz。Windows では LlamaZenzScorer)。</summary>
public interface IConversionScorer
{
    /// <summary>採点できる状態か (モデルの読み込みが終わっている)。</summary>
    bool IsReady { get; }

    /// <summary>まだならモデルの読み込みを別スレッドで始める。ランタイムが無ければ何もしない。</summary>
    void EnsureLoading();

    /// <summary>
    /// 文脈と読み (ひらがな) のもとで、各候補の対数確率 log P(候補 | 文脈, 読み) を候補の順に返す。
    /// 採点できない・失敗した・deadline (Environment.TickCount64 のミリ秒) を過ぎたら null。
    /// </summary>
    IReadOnlyList<double>? Score(string context, string reading, IReadOnlyList<string> candidates, long deadline);
}

/// <summary>MeltKelp: zenz の並べ替えの調整値。テストと計測から変えられるようにしておく (imev では事前の効きを切り分けにくかった)。</summary>
public sealed record ZenzRerankOptions
{
    /// <summary>採点する候補の数 (変換エンジンの上位から)。</summary>
    public int MaxCandidates { get; init; } = 5;

    /// <summary>
    /// 元の順位の事前 (1 つ下がるごとに対数確率から引く量、nat)。大きいほど変換エンジンの順を保つ。
    /// imev の「-500 × 対数確率 + 300 × 順位」と同じ比 (0.6)。
    /// </summary>
    public double RankPrior { get; init; } = 0.6;

    /// <summary>文脈の上限 (文字)。前の文節を足し込んだ後の文字列に掛ける。</summary>
    public int MaxContextChars { get; init; } = 20;

    /// <summary>1 回の変換 (ConvertClauses) で採点に使ってよい時間の合計 (ミリ秒)。UI スレッドで呼ばれるため。</summary>
    public int BudgetMs { get; init; } = 150;

    /// <summary>採点の結果 (候補ごと) を覚えておく数。超えたら捨てて覚え直す。</summary>
    public int CacheSize { get; init; } = 2048;
}

/// <summary>
/// MeltKelp: 変換エンジン (HybridConverter) を包み、各文節の最初の候補を zenz で文脈つきに選び直す (MeltKelp #2)。
///   ・最初の候補は ConvertClauses の各文節の Text で決まるので、並べ替えの本体はここ。Candidates (↑↓・Space の連打で出す一覧) は、
///     同じ読みを最後に採点した結果で並べ替えるだけ (採点し直さない)
///   ・文脈は、受け取ったキャレットの前の文字列の最後の行 + 同じ変換の中の前の文節 (選び直した後のもの)。上限は足し込んだ後に掛ける
///   ・文脈が空・モデルが無い・読み込み中・採点の失敗・時間切れ・例外では、変換エンジンの結果をそのまま返す (フェイルオープン)
///   ・かなだけの候補も採点する。imev では読みを写したカタカナ (ゲキダン) が勝つので外していたが、原因は imev のプロンプトの
///     タグの誤り (LlamaZenzScorer) で、正しいタグでは文脈つきの写しは低く出る。かなが正解の語 (したい・やって) は、かなが勝つ
///   ・学習は変えない: Learn はそのまま渡す。Meltype の確定履歴・ユーザー辞書・文脈の手がかりは、CompositionController がこの後で効かせる
/// 文節の数・読み・区切りは変えない (Text だけを差し替える) ので、候補を欠かしたり増やしたりしない。
/// </summary>
public sealed class ZenzRerankingConverter(IKanjiConverter inner, Func<string, IReadOnlyList<string>> candidates, IConversionScorer scorer,
    Func<bool> enabled, ZenzRerankOptions? options = null) : IKanjiConverter, ILearningConverter, IPrecedingTextReceiver
{
    private readonly ZenzRerankOptions _options = options ?? new ZenzRerankOptions();
    // (文脈, 読み, 候補) → 対数確率。候補ごとに控え、足りない候補だけを採点する (対数確率は候補ごとに別の系列で求めるので、
    // まとめ方で値は変わらない)。失敗・時間切れは NaN (同じ入力で何度も時間を使わないため、失敗も覚える)
    private readonly Dictionary<(string Context, string Reading, string Candidate), double> _scores = [];
    // 読み → 最後に採点できたときの文脈 (Candidates の並べ替えに使う)。採点できなかった変換では消す
    private readonly Dictionary<string, string> _lastContext = new(StringComparer.Ordinal);
    private string? _precedingText;
    // 1 回の変換 (ConvertJapanese → SetPrecedingText) で採点に使ってよい時間の締め切り。CompositionController.ConvertWithContext は
    // ConvertClauses を文脈あり・なしで 2 回呼ぶので、呼ぶたびに取り直すと UI スレッドを予算の 2 倍止める (agy レビュー)
    private long? _deadline;

    /// <summary>
    /// HybridConverter を包む (候補の一覧も HybridConverter から取る)。アプリの起動時に作るので、ここでモデルの読み込みを始める
    /// (最初の変換で始めると、起動直後の 1 回目の変換が並べ替わらない)。
    /// </summary>
    public ZenzRerankingConverter(HybridConverter inner, IConversionScorer scorer, Func<bool> enabled, ZenzRerankOptions? options = null)
        : this(inner, inner.Candidates, scorer, enabled, options)
    {
        if (enabled()) scorer.EnsureLoading();
    }

    /// <summary>変換を頼む側が、変換の直前に呼ぶ。採点に使ってよい時間 (ZenzRerankOptions.BudgetMs) もここから数える。</summary>
    public void SetPrecedingText(string? text)
    {
        _precedingText = text;
        _deadline = Environment.TickCount64 + _options.BudgetMs;
    }

    /// <summary>採点の結果の控えを捨てる (--perf の cold で、変換エンジンの控えと一緒に捨てる)。</summary>
    internal void ClearCache()
    {
        _scores.Clear();
        _lastContext.Clear();
    }

    public string? Convert(string hiragana) => inner.Convert(hiragana);

    public void Learn(string? context, IReadOnlyList<ConversionClause> clauses)
    {
        if (inner is ILearningConverter learner) learner.Learn(context, clauses);
    }

    public IReadOnlyList<ConversionClause>? ConvertClauses(string hiragana, string? context = null)
    {
        var clauses = inner.ConvertClauses(hiragana, context);
        if (clauses is null || clauses.Count == 0) return clauses;
        // 前の変換で採点した順は、今回採点し直せたときだけ使う (文脈が空・時間切れ・失敗で、前の文脈の順を一覧に残さない)
        foreach (var clause in clauses) _lastContext.Remove(clause.Reading);
        if (!enabled()) return clauses;
        try
        {
            if (!scorer.IsReady)
            {
                scorer.EnsureLoading();
                return clauses;
            }
            return Rerank(clauses);
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Warn($"zenz の並べ替えに失敗したので、変換エンジンの順のまま使います: {ex.GetType().Name}");
            return clauses;
        }
    }

    /// <summary>
    /// 読みの候補の一覧。同じ読みを最後に採点できていれば、そのときの文脈の採点で、採点した候補どうしの位置の中で並べ替える
    /// (事前は一覧の中の位置)。採点していない候補の位置は動かさない。
    /// </summary>
    public IReadOnlyList<string> Candidates(string reading)
    {
        var list = candidates(reading);
        if (!enabled() || !_lastContext.TryGetValue(reading, out var context)) return list;
        var slots = new List<int>();
        for (var i = 0; i < list.Count; i++)
            if (_scores.TryGetValue((context, reading, list[i]), out var score) && !double.IsNaN(score)) slots.Add(i);
        if (slots.Count < 2) return list;
        var ordered = slots
            .OrderByDescending(i => _scores[(context, reading, list[i])] - _options.RankPrior * i)
            .ThenBy(i => i)
            .Select(i => list[i])
            .ToList();
        var result = list.ToList();
        for (var j = 0; j < slots.Count; j++) result[slots[j]] = ordered[j];
        return result;
    }

    private IReadOnlyList<ConversionClause> Rerank(IReadOnlyList<ConversionClause> clauses)
    {
        var deadline = _deadline ?? Environment.TickCount64 + _options.BudgetMs;
        var result = new List<ConversionClause>(clauses.Count);
        // 前の行は使わない: メモ帳の別の行・チャットの別のメッセージなど関係の無いことが多く、引っぱられる
        // (前の行「著名な研究者の講演」+ 街の + こうえん → 公演 になった)。文をまたぐ (。の前) のは使う
        var preceding = _precedingText ?? "";
        var context = preceding[(preceding.LastIndexOfAny(['\n', '\r']) + 1)..];
        foreach (var clause in clauses)
        {
            var text = Environment.TickCount64 < deadline ? Choose(clause, Tail(context, _options.MaxContextChars), deadline) : clause.Text;
            result.Add(text == clause.Text ? clause : clause with { Text = text });
            // 上限は使う直前 (Choose に渡すとき) に掛ける。ここで足していくと伸びる (imev PR #12)
            context = Tail(context + text, _options.MaxContextChars * 4);
        }
        return result;
    }

    /// <summary>文節の最初の候補を選ぶ。選び直さないときは今の Text を返す。</summary>
    private string Choose(ConversionClause clause, string context, long deadline)
    {
        if (context.Length == 0 || !IsHiraganaReading(clause.Reading)) return clause.Text;
        var list = new List<string> { clause.Text };
        foreach (var candidate in candidates(clause.Reading))
        {
            if (list.Count >= _options.MaxCandidates) break;
            if (!list.Contains(candidate)) list.Add(candidate);
        }
        if (list.Count < 2) return clause.Text;

        var missing = list.Where(c => !_scores.ContainsKey((context, clause.Reading, c))).ToList();
        if (missing.Count > 0)
        {
            if (Environment.TickCount64 >= deadline) return clause.Text;
            var watch = Stopwatch.StartNew();
            var scored = scorer.Score(context, clause.Reading, missing, deadline);
            if (scored is not null && scored.Count != missing.Count) scored = null;
            if (_scores.Count >= _options.CacheSize) _scores.Clear();
            for (var i = 0; i < missing.Count; i++) _scores[(context, clause.Reading, missing[i])] = scored?[i] ?? double.NaN;
            Diagnostics.Log.Info(scored is null
                ? $"zenz: 採点できませんでした (文脈 {context.Length} 文字・候補 {missing.Count} 件・{watch.ElapsedMilliseconds} ms)。変換エンジンの順のまま"
                : $"zenz: 採点しました (文脈 {context.Length} 文字・候補 {missing.Count} 件・{watch.ElapsedMilliseconds} ms): {string.Join(" ", scored.Select(x => x.ToString("F2")))}");
        }
        var scores = list.Select(c => _scores.TryGetValue((context, clause.Reading, c), out var x) ? x : double.NaN).ToArray();
        if (scores.Any(double.IsNaN)) return clause.Text;

        // 対数確率 - 事前 × 元の順位 の大きい順。同点なら元の順 (安定)
        var best = Enumerable.Range(0, list.Count)
            .OrderByDescending(j => scores[j] - _options.RankPrior * j)
            .ThenBy(j => j)
            .First();
        if (_lastContext.Count >= _options.CacheSize) _lastContext.Clear();
        _lastContext[clause.Reading] = context;
        return list[best];
    }

    /// <summary>末尾の n 文字 (サロゲートペアを割らない)。</summary>
    internal static string Tail(string text, int n)
    {
        if (text.Length <= n) return text;
        var start = text.Length - n;
        if (char.IsLowSurrogate(text[start])) start++;
        return text[start..];
    }

    /// <summary>ひらがな (と長音) だけの読みか。記号・英数字の文節 (、 3) は採点しない。</summary>
    private static bool IsHiraganaReading(string reading) =>
        reading.Length > 0 && reading.All(c => c is (>= 'ぁ' and <= 'ゖ') or 'ー' or 'ゝ' or 'ゞ');
}
