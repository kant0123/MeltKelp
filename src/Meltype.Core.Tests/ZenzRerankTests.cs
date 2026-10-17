// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

using Meltype.Composition;

namespace Meltype.Tests;

/// <summary>MeltKelp #2: zenz の並べ替え (ZenzRerankingConverter)。LM は FakeScorer で置き換える。</summary>
internal static class ZenzRerankTests
{
    /// <summary>決めた候補に高い対数確率を付ける採点器。呼ばれた文脈を覚える。</summary>
    private sealed class FakeScorer : IConversionScorer
    {
        public bool Ready { get; set; } = true;
        public bool IsReady => Ready;
        public int Loads { get; private set; }
        public List<(string Context, string Reading, string[] Candidates)> Calls { get; } = [];
        public Func<string, string, IReadOnlyList<string>, IReadOnlyList<double>?> Scores { get; set; } = (_, _, c) => c.Select(_ => -1.0).ToArray();

        public void EnsureLoading() => Loads++;

        public IReadOnlyList<double>? Score(string context, string reading, IReadOnlyList<string> candidates, long deadline)
        {
            Calls.Add((context, reading, candidates.ToArray()));
            return Scores(context, reading, candidates);
        }

        /// <summary>文脈に key が含まれていれば word を、なければ先頭を強く推す。</summary>
        public static FakeScorer Prefer(string key, string word) => new()
        {
            Scores = (context, _, c) => c.Select((x, i) => context.Contains(key) ? (x == word ? -0.1 : -8.0) : (i == 0 ? -0.1 : -8.0)).ToArray(),
        };
    }

    /// <summary>決まった文節を返す変換エンジン。</summary>
    private sealed class FixedConverter(params ConversionClause[] clauses) : IKanjiConverter, ILearningConverter
    {
        public List<IReadOnlyList<ConversionClause>> Learned { get; } = [];
        public string? Convert(string hiragana) => string.Concat(clauses.Select(c => c.Text));
        public IReadOnlyList<ConversionClause>? ConvertClauses(string hiragana, string? context = null) => clauses;
        public void Learn(string? context, IReadOnlyList<ConversionClause> learned) => Learned.Add(learned);
    }

    private static readonly Dictionary<string, string[]> Lists = new()
    {
        ["こうえん"] = ["公園", "講演", "公演", "後援", "好演", "コウエン"],
        ["げきだんの"] = ["劇団の", "げきだんの"],
        ["したい"] = ["したい", "死体", "肢体", "姿態"],
    };

    private static ConversionClause C(string reading, string text) => new(reading, text);

    private static IReadOnlyList<string> Candidates(string reading) => Lists.TryGetValue(reading, out var list) ? list : [];

    private static ZenzRerankingConverter Create(IKanjiConverter inner, IConversionScorer scorer, bool enabled = true, ZenzRerankOptions? options = null) =>
        new(inner, Candidates, scorer, () => enabled, options);

    [Test]
    public static void Rerank_ChoosesByPrecedingText()
    {
        var scorer = FakeScorer.Prefer("千秋楽", "公演");
        var converter = Create(new FixedConverter(C("こうえん", "公園")), scorer);
        converter.SetPrecedingText("劇団の千秋楽の");
        Assert.Equal("公演", converter.ConvertClauses("こうえん")!.Single().Text);
        Assert.Equal("劇団の千秋楽の", scorer.Calls.Single().Context);
        Assert.Equal("公園,講演,公演,後援,好演", string.Join(",", scorer.Calls.Single().Candidates), "上位 5 件");
    }

    [Test]
    public static void Rerank_WithoutContext_KeepsEngineOrder()
    {
        // 文脈が無ければ採点しない (導入前と同じ)
        var scorer = FakeScorer.Prefer("", "公演");
        var converter = Create(new FixedConverter(C("こうえん", "公園")), scorer);
        converter.SetPrecedingText(null);
        Assert.Equal("公園", converter.ConvertClauses("こうえん")!.Single().Text);
        converter.SetPrecedingText("");
        Assert.Equal("公園", converter.ConvertClauses("こうえん")!.Single().Text);
        Assert.Equal(0, scorer.Calls.Count, "採点しない");
        Assert.Equal("公園,講演,公演,後援,好演,コウエン", string.Join(",", converter.Candidates("こうえん")), "候補の一覧も変換エンジンの順");
    }

    [Test]
    public static void Rerank_WithoutRuntime_KeepsEngineOrder()
    {
        // ランタイムが無い・読み込み中: 採点せず、読み込みを始めるだけ
        var scorer = FakeScorer.Prefer("千秋楽", "公演");
        scorer.Ready = false;
        var converter = Create(new FixedConverter(C("こうえん", "公園")), scorer);
        converter.SetPrecedingText("劇団の千秋楽の");
        Assert.Equal("公園", converter.ConvertClauses("こうえん")!.Single().Text);
        Assert.Equal(0, scorer.Calls.Count, "採点しない");
        Assert.Equal(1, scorer.Loads, "読み込みを始める");
    }

    [Test]
    public static void Rerank_Disabled_KeepsEngineOrder()
    {
        var scorer = FakeScorer.Prefer("千秋楽", "公演");
        var converter = Create(new FixedConverter(C("こうえん", "公園")), scorer, enabled: false);
        converter.SetPrecedingText("劇団の千秋楽の");
        Assert.Equal("公園", converter.ConvertClauses("こうえん")!.Single().Text);
        Assert.Equal(0, scorer.Calls.Count + scorer.Loads, "設定が OFF なら読み込みもしない");
    }

    [Test]
    public static void Rerank_FailureOrTimeout_KeepsEngineOrderAndIsCached()
    {
        // 採点の失敗・時間切れ (null) は元の順。同じ入力で何度も時間を使わない
        var scorer = new FakeScorer { Scores = (_, _, _) => null };
        var converter = Create(new FixedConverter(C("こうえん", "公園")), scorer);
        converter.SetPrecedingText("劇団の千秋楽の");
        Assert.Equal("公園", converter.ConvertClauses("こうえん")!.Single().Text);
        Assert.Equal("公園", converter.ConvertClauses("こうえん")!.Single().Text);
        Assert.Equal(1, scorer.Calls.Count, "失敗も覚える");

        // 件数の合わない結果も失敗として扱う
        scorer = new FakeScorer { Scores = (_, _, _) => [-0.1] };
        converter = Create(new FixedConverter(C("こうえん", "公園")), scorer);
        converter.SetPrecedingText("劇団の千秋楽の");
        Assert.Equal("公園", converter.ConvertClauses("こうえん")!.Single().Text);
    }

    [Test]
    public static void Rerank_Exception_KeepsEngineOrder()
    {
        var scorer = new FakeScorer { Scores = (_, _, _) => throw new InvalidOperationException() };
        var converter = Create(new FixedConverter(C("げきだんの", "劇団の"), C("こうえん", "公園")), scorer);
        converter.SetPrecedingText("千秋楽の");
        var clauses = converter.ConvertClauses("げきだんのこうえん")!;
        Assert.Equal("劇団の|公園", string.Join("|", clauses.Select(c => c.Text)), "候補を欠かさない");
        Assert.Equal("げきだんの|こうえん", string.Join("|", clauses.Select(c => c.Reading)), "区切りを変えない");
    }

    [Test]
    public static void Rerank_OutOfBudget_KeepsEngineOrder()
    {
        // 使ってよい時間が残っていなければ採点しない (UI スレッドを止めない)
        var scorer = FakeScorer.Prefer("千秋楽", "公演");
        var converter = Create(new FixedConverter(C("こうえん", "公園")), scorer, options: new ZenzRerankOptions { BudgetMs = -1 });
        converter.SetPrecedingText("劇団の千秋楽の");
        Assert.Equal("公園", converter.ConvertClauses("こうえん")!.Single().Text);
        Assert.Equal(0, scorer.Calls.Count);
    }

    [Test]
    public static void Rerank_PreviousClausesAreContext()
    {
        // キャレットの前の文字が無くても、同じ変換の前の文節を文脈にする (劇団の + こうえん)
        var scorer = FakeScorer.Prefer("劇団", "公演");
        var converter = Create(new FixedConverter(C("げきだんの", "劇団の"), C("こうえん", "公園")), scorer);
        converter.SetPrecedingText(null);
        var clauses = converter.ConvertClauses("げきだんのこうえん")!;
        Assert.Equal("劇団の|公演", string.Join("|", clauses.Select(c => c.Text)));
        Assert.Equal("劇団の", scorer.Calls.Single().Context, "最初の文節は文脈が空なので採点しない");
    }

    [Test]
    public static void Rerank_PrecedingLinesAreNotContext()
    {
        // キャレットの前の文字のうち、前の行は文脈にしない (前の行の 講演 に引っぱられて 街の + こうえん → 公演 になった)
        var scorer = FakeScorer.Prefer("講演", "公演");
        var converter = Create(new FixedConverter(C("まちの", "街の"), C("こうえん", "公園")), scorer);
        foreach (var lineBreak in new[] { "\n", "\r\n" })
        {
            converter.SetPrecedingText("著名な研究者の講演" + lineBreak);
            Assert.Equal("街の|公園", string.Join("|", converter.ConvertClauses("まちのこうえん")!.Select(c => c.Text)));
        }
        Assert.Equal("街の", scorer.Calls.Single().Context, "最初の文節は文脈が空 (前の行だけ) なので採点しない。2 回目はキャッシュ");

        // 同じ行の中なら、文をまたいでも使う
        converter.SetPrecedingText("劇団の千秋楽を見た。研究者の講演");
        converter.ConvertClauses("まちのこうえん");
        Assert.True(scorer.Calls[^1].Context.EndsWith("見た。研究者の講演街の"), scorer.Calls[^1].Context);
    }

    [Test]
    public static void Rerank_ContextLimitAppliesAfterAppending()
    {
        // 上限は前の文節を足し込んだ後に掛ける (imev PR #12: 入口だけで切って、追記で伸びていた)
        var options = new ZenzRerankOptions { MaxContextChars = 8 };
        var scorer = new FakeScorer();
        var many = Enumerable.Range(0, 6).Select(_ => new ConversionClause("こうえん", "公園")).ToArray();
        var converter = Create(new FixedConverter(many), scorer, options: options);
        converter.SetPrecedingText(new string('あ', 30));
        converter.ConvertClauses(string.Concat(many.Select(c => c.Reading)));
        Assert.True(scorer.Calls.Count >= 2, "文節ごとに採点する (文脈が上限で同じになった後はキャッシュ)");
        Assert.True(scorer.Calls.All(c => c.Context.Length <= 8), string.Join(",", scorer.Calls.Select(c => c.Context.Length)));
        Assert.Equal("公園公園公園公園", scorer.Calls[^1].Context, "前の文節が末尾に入る");
    }

    [Test]
    public static void Rerank_KanaCandidatesAreScored()
    {
        // かなだけの候補も採点する: 変換エンジンが漢字にした語尾・補助動詞を、文脈でかなに戻せる (imev #31: したい → 死体)
        var scorer = FakeScorer.Prefer("買い物", "したい");
        var converter = Create(new FixedConverter(C("したい", "死体")), scorer);
        converter.SetPrecedingText("週末に買い物を");
        Assert.Equal("したい", converter.ConvertClauses("したい")!.Single().Text);
        Assert.Equal("死体,したい,肢体,姿態", string.Join(",", scorer.Calls.Single().Candidates), "最初の候補 (文節の Text) を先頭に、重ねずに足す");
    }

    [Test]
    public static void Rerank_SingleCandidate_IsNotScored()
    {
        var scorer = new FakeScorer();
        var converter = Create(new FixedConverter(C("ほげ", "ほげ")), scorer);
        converter.SetPrecedingText("それで");
        Assert.Equal("ほげ", converter.ConvertClauses("ほげ")!.Single().Text);
        Assert.Equal(0, scorer.Calls.Count, "候補が 1 つなら採点しない");
    }

    [Test]
    public static void Rerank_NonHiraganaReading_IsNotScored()
    {
        // 記号・数字の文節 (、 3) は採点しない
        var scorer = new FakeScorer();
        var converter = Create(new FixedConverter(C("、", "、"), C("3", "3")), scorer);
        converter.SetPrecedingText("それで");
        converter.ConvertClauses("、3");
        Assert.Equal(0, scorer.Calls.Count);
    }

    [Test]
    public static void Rerank_CandidatesFollowLastRanking()
    {
        // ↑↓ で出す一覧も、直前に採点した順に並べ替える (採点した候補どうしの位置の中だけ。件数・重複は変えない)
        var scorer = new FakeScorer { Scores = (_, _, c) => c.Select(x => x switch { "公演" => -0.1, "講演" => -1.0, _ => -9.0 }).ToArray() };
        var converter = Create(new FixedConverter(C("こうえん", "公園")), scorer);
        converter.SetPrecedingText("劇団の千秋楽の");
        Assert.Equal("公演", converter.ConvertClauses("こうえん")!.Single().Text);
        var list = converter.Candidates("こうえん");
        Assert.Equal("公演", list[0]);
        Assert.Equal("講演", list[1]);
        Assert.Equal(6, list.Count);
        Assert.Equal(list.Count, list.Distinct().Count(), "重複させない");
        Assert.Equal("コウエン", list[^1], "採点しなかった候補 (上位 5 件の外) の位置は動かさない");
    }

    [Test]
    public static void Rerank_CandidatesUnchangedWhenListChanged()
    {
        // 採点した後に一覧が変わったら (Mozc の学習・変換エンジンの切り替え) 並べ替えない。変換できなければ null のまま
        var lists = new Dictionary<string, string[]> { ["こうえん"] = ["公園", "講演", "公演"] };
        var scorer = new FakeScorer { Scores = (_, _, c) => c.Select(x => x == "公演" ? -0.1 : -9.0).ToArray() };
        var converter = new ZenzRerankingConverter(new FixedConverter(C("こうえん", "公園")), r => lists[r], scorer, () => true);
        converter.SetPrecedingText("劇団の千秋楽の");
        Assert.Equal("公演", converter.ConvertClauses("こうえん")!.Single().Text);
        Assert.Equal("公演", converter.Candidates("こうえん")[0]);
        lists["こうえん"] = ["講演", "公園"];
        Assert.Equal("講演,公園", string.Join(",", converter.Candidates("こうえん")));

        var none = new ZenzRerankingConverter(new NullConverter(), _ => [], scorer, () => true);
        none.SetPrecedingText("それで");
        Assert.True(none.ConvertClauses("ほげ") is null, "変換エンジンが null なら null");
    }

    private sealed class NullConverter : IKanjiConverter
    {
        public string? Convert(string hiragana) => null;
        public IReadOnlyList<ConversionClause>? ConvertClauses(string hiragana, string? context = null) => null;
    }

    [Test]
    public static void Rerank_RankPriorKeepsEngineOrderWhenClose()
    {
        // 対数確率の差が事前 (元の順位の差) より小さければ動かさない
        var scorer = new FakeScorer { Scores = (_, _, c) => c.Select(x => x == "公演" ? -1.0 : -2.0).ToArray() };
        var converter = Create(new FixedConverter(C("こうえん", "公園")), scorer, options: new ZenzRerankOptions { RankPrior = 1.0 });
        converter.SetPrecedingText("劇団の千秋楽の");
        Assert.Equal("公園", converter.ConvertClauses("こうえん")!.Single().Text, "公演 は 2 つ下 (事前 2.0) で差 1.0 なので動かない");
    }

    [Test]
    public static void HybridConstructor_StartsLoadingOnlyWhenEnabled()
    {
        // アプリで使う HybridConverter を包む形は、作った時点で読み込みを始める (起動直後の 1 回目から並べ替える)
        var hybrid = new HybridConverter(() => Meltype.Config.ConversionEngine.System, null, new FixedConverter(C("こうえん", "公園")), null);
        var scorer = new FakeScorer();
        _ = new ZenzRerankingConverter(hybrid, scorer, () => true);
        Assert.Equal(1, scorer.Loads);
        var off = new FakeScorer();
        _ = new ZenzRerankingConverter(hybrid, off, () => false);
        Assert.Equal(0, off.Loads, "設定が OFF なら読み込まない");
    }

    [Test]
    public static void Learn_PassesThrough()
    {
        var inner = new FixedConverter(C("こうえん", "公園"));
        var converter = Create(inner, new FakeScorer());
        converter.Learn(null, [new("こうえん", "公演")]);
        Assert.Equal("公演", inner.Learned.Single().Single().Text);
    }

    [Test]
    public static void Controller_PassesPrecedingTextAndLearnedChoiceStaysFirst()
    {
        // CompositionController が変換の前にキャレットの前の文字を渡す。選び直した変換の学習 (確定履歴) は zenz より優先する
        var more = new Dictionary<string, string[]> { ["はしを"] = ["橋を", "箸を", "端を"] };
        // 文脈は同梱の手がかり辞書 (料理 → 箸) に当たらないものにする (手がかり辞書も zenz の後で効く)
        var scorer = FakeScorer.Prefer("それで", "箸を");
        var history = new ConversionHistory(null);
        var zenz = new ZenzRerankingConverter(new CompositionTests.FakeConverter(), r => more.TryGetValue(r, out var l) ? l : [], scorer, () => true);
        var k = new CompositionTests.Keyboard(history: history, converter: zenz, moreCandidates: zenz.Candidates);
        k.Host.PrecedingText = "それで";
        k.Type("hashiwo ");
        Assert.Equal("箸を", k.Host.View!.Clauses![0], "zenz が文脈で選び直す");
        Assert.True(scorer.Calls.Any(c => c.Context == "それで"), string.Join(",", scorer.Calls.Select(c => c.Context)));

        history.Remember("はしを", "端を");
        k = new CompositionTests.Keyboard(history: history, converter: zenz, moreCandidates: zenz.Candidates);
        k.Host.PrecedingText = "それで";
        k.Type("hashiwo ");
        Assert.Equal("端を", k.Host.View!.Clauses![0], "学習した候補は zenz の後でも先頭");
    }

    [Test]
    public static void Tail_DoesNotSplitSurrogatePair()
    {
        Assert.Equal("b", ZenzRerankingConverter.Tail("a😊b", 2), "サロゲートペアの片割れを残さない");
        Assert.Equal("😊b", ZenzRerankingConverter.Tail("a😊b", 3));
    }
}
