// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

using Meltype.Composition;

namespace Meltype.Tests;

/// <summary>MeltKelp #49: 英語として覚えた短い語 (no) が、英字の略語の後ろの助詞 (APIno) まで英字にしていた。</summary>
internal static class ApiParticleTests
{
    /// <summary>learned の語を覚えさせた検出器で打つ (検出器は共有なので、終わったら学習を外す)。</summary>
    private static string Typed(string typed, string? learned, bool explicitChoice = true, string? before = null)
    {
        var memory = new LanguageMemory(null);
        if (learned is not null) memory.Remember(learned, english: true, explicitChoice);
        var saved = CompositionTests.Detector.Memory;
        CompositionTests.Detector.Memory = memory;
        try
        {
            var k = new CompositionTests.Keyboard(languages: memory);
            if (before is not null) k.Host.PrecedingText = before;
            k.Type(typed + "\n");
            return k.Host.Document;
        }
        finally
        {
            CompositionTests.Detector.Memory = saved;
        }
    }

    [Test]
    public static void LearnedShortEnglishWord_DoesNotTurnParticleAfterAbbreviationIntoEnglish()
    {
        // 実機で no を英字に直して覚えた状態 (F10)。APIno が APIno のまま確定されていた
        Assert.Equal("APIの", Typed("APIno", "no"), "学習した no が API の後ろの助詞を英字にしない");
        Assert.Equal("APIのように", Typed("APInoyouni", "no"), "後ろに日本語が続いても");
        Assert.Equal("APIには", Typed("APIniha", "ni"), "ni でも同じ");
        Assert.Equal("APIの", Typed("APIno", "no", explicitChoice: false), "変換の候補から選んだだけの学習でも");
    }

    [Test]
    public static void LearnedShortEnglishWord_StillWorksStandalone()
    {
        // 覚えさせた効果そのものは残す: 単独で打った no・英文の続きの no は英字
        Assert.Equal("no", Typed("no", "no"), "単独の no は英字のまま");
        Assert.Equal("no", Typed("no", "no", before: "Answer: "), "英文の後ろの no も英字");
    }
}
