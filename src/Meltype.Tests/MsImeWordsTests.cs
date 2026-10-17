// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

namespace Meltype.Tests;

/// <summary>MeltKelp #27: 打ったキーのひらがなを Microsoft IME の形態素解析で単語に区切る。</summary>
internal static class MsImeWordsTests
{
    [Test]
    public static void Split_ByMorphemes()
    {
        using var words = new Composition.MsImeWords();
        Assert.True(words.Split("") is null, "空は区切らない");
        using (var ime = new Composition.MsImeKanjiConverter())
        {
            if (ime.Convert("てすと") is null) return; // Microsoft IME が無い環境
        }
        const string reading = "きょうはいいてんきです";
        var split = words.Split(reading);
        Assert.True(split is { Count: > 1 }, "単語に区切る: " + (split is null ? "(null)" : string.Join(" ", split)));
        Assert.Equal(reading, string.Concat(split!), "つなげると元の読み");
        Assert.True(split!.Contains("は"), "助詞を分ける: " + string.Join(" ", split));
        Assert.True(ReferenceEquals(split, words.Split(reading)), "同じ読みは前の結果を使う");

        // 100 文字を超える読み (Microsoft IME が受け付けない長さ) も区切ってつなげる
        var sentence = string.Concat(Enumerable.Repeat("きょうはいいてんきですね、あしたもはれるといいですね。", 6));
        var longSplit = words.Split(sentence);
        Assert.True(longSplit is { Count: > 6 }, "長い読み");
        Assert.Equal(sentence, string.Concat(longSplit!));
    }
}
