// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

using System.Diagnostics;
using Meltype.Composition;

namespace Meltype.Tests;

/// <summary>
/// MeltKelp #2: 実際の zenz のランタイム (%LOCALAPPDATA%\Meltype\zenz) で採点を確かめる。ランタイムが無ければ何もしない (CI など)。
/// 例文は自分で用意したもの (imev で実機で並び替わるのを確かめた 3 つ)。
/// </summary>
internal static class ZenzScorerTests
{
    private static readonly Lazy<LlamaZenzScorer?> Shared = new(() =>
    {
        var scorer = new LlamaZenzScorer();
        if (!scorer.IsInstalled) return null;
        Assert.True(scorer.WaitUntilReady(60_000), "ランタイムがあるのに読み込めない");
        return scorer;
    });

    private static readonly string[] Kouen = ["公園", "講演", "公演", "後援", "好演"];

    [Test]
    public static void RealRuntime_ChoosesByContext()
    {
        if (Shared.Value is not { } scorer) return;
        foreach (var (context, expected) in new[] { ("劇団の千秋楽の", "公演"), ("著名な研究者の", "講演"), ("街の", "公園") })
        {
            var watch = Stopwatch.StartNew();
            var scores = scorer.Score(context, "こうえん", Kouen, Environment.TickCount64 + 5_000);
            watch.Stop();
            Assert.True(scores is not null, $"{context}: 採点できない");
            var best = Kouen[scores!.Select((s, i) => (s, i)).MaxBy(x => x.s).i];
            Console.WriteLine($"        {context} + こうえん: {string.Join(" ", Kouen.Zip(scores!, (c, s) => $"{c}={s:F2}"))} ({watch.ElapsedMilliseconds} ms)");
            Assert.Equal(expected, best, $"{context} + こうえん");
        }
    }

    [Test]
    public static void RealRuntime_KanaCandidates()
    {
        if (Shared.Value is not { } scorer) return;
        // 文脈があれば、読みを写したカタカナは勝たない (imev ではプロンプトのタグの誤りで勝っていた)。かなが正解の語ではかなが勝つ
        foreach (var (context, reading, candidates, expected) in new[]
        {
            ("有名な", "げきだん", new[] { "劇団", "ゲキダン", "げきだん" }, "劇団"),
            ("週末に買い物を", "したい", ["死体", "したい", "肢体"], "したい"),
            ("宿題を", "やって", ["遣って", "やって", "ヤッテ"], "やって"),
            ("明日の", "てすと", ["てすと", "テスト"], "テスト"),
        })
        {
            var scores = scorer.Score(context, reading, candidates, Environment.TickCount64 + 5_000);
            Assert.True(scores is not null, $"{context}: 採点できない");
            Console.WriteLine($"        {context} + {reading}: {string.Join(" ", candidates.Zip(scores!, (c, s) => $"{c}={s:F2}"))}");
            Assert.Equal(expected, candidates[scores!.Select((s, i) => (s, i)).MaxBy(x => x.s).i], $"{context} + {reading}");
        }
    }

    [Test]
    public static void RealRuntime_TimesOut()
    {
        if (Shared.Value is not { } scorer) return;
        // 締め切りを過ぎていれば decode を打ち切って null。その後も普通に採点できる (KV を壊していない)
        Assert.True(scorer.Score("長い文脈を毎回変えてキャッシュを外す" + Guid.NewGuid(), "こうえん", Kouen, Environment.TickCount64 - 1) is null, "締め切り切れで null");
        Assert.True(scorer.Score("街の", "こうえん", Kouen, Environment.TickCount64 + 5_000) is { Count: 5 }, "打ち切りの後も採点できる");
    }

    [Test]
    public static void RealRuntime_RerankingConverterUsesPrecedingText()
    {
        if (Shared.Value is not { } scorer) return;
        var inner = new FixedConverter();
        var converter = new ZenzRerankingConverter(inner, _ => Kouen, scorer, () => true);
        converter.SetPrecedingText("劇団の千秋楽の");
        Assert.Equal("公演", converter.ConvertClauses("こうえん")!.Single().Text, "キャレットの前の文字を文脈にする");
        converter.SetPrecedingText(null);
        Assert.Equal("公園", converter.ConvertClauses("こうえん")!.Single().Text, "文脈が無ければ変換エンジンのまま");

        // 実機で起きたこと: Mozc が 公演 を覚えて先頭に出し、メモ帳の前の行に「著名な研究者の講演」がある。前の行を文脈に入れると 公演 のままだった
        string[] learned = ["公演", "公園", "講演", "後援", "好演"];
        var mozcLike = new ZenzRerankingConverter(new LearnedConverter(), _ => learned, scorer, () => true);
        mozcLike.SetPrecedingText("劇団の千秋楽の公演" + Environment.NewLine + "著名な研究者の講演" + Environment.NewLine);
        Assert.Equal("街の|公園", string.Join("|", mozcLike.ConvertClauses("まちのこうえん")!.Select(c => c.Text)), "前の行は文脈にしない");
    }

    private sealed class LearnedConverter : IKanjiConverter
    {
        public string? Convert(string hiragana) => "街の公演";
        public IReadOnlyList<ConversionClause>? ConvertClauses(string hiragana, string? context = null) => [new("まちの", "街の"), new("こうえん", "公演")];
    }

    private sealed class FixedConverter : IKanjiConverter
    {
        public string? Convert(string hiragana) => "公園";
        public IReadOnlyList<ConversionClause>? ConvertClauses(string hiragana, string? context = null) => [new(hiragana, "公園")];
    }
}
