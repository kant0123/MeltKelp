// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

using Meltype.Composition;
using Meltype.Config;
using Meltype.Input;

namespace Meltype.Tests;

/// <summary>MeltKelp #27: 設定「打ったキーを表示」の形式 (ローマ字・表示しない・ひらがな・単語ごとに空白を入れたひらがな)。</summary>
internal static class TypedKeysFormatTests
{
    private static CompositionText Text(string keys)
    {
        var text = new CompositionText(CompositionTests.Detector);
        foreach (var c in keys) text.Append(c);
        return text;
    }

    /// <summary>形態素解析の代わり。知っている読みだけ区切る。</summary>
    private static IReadOnlyList<string>? Words(string kana) => kana switch
    {
        "きょうは" => ["きょう", "は"],
        "きょうはいいてんきです" => ["きょう", "は", "いい", "てんき", "です"],
        _ => null,
    };

    [Test]
    public static void Romaji_IsTypedKeys()
    {
        Assert.Equal("kyouhagoogle", TypedKeysFormatter.Format(Text("kyouhagoogle"), TypedKeysStyle.Romaji));
    }

    [Test]
    public static void None_AndEmpty_AreNull()
    {
        Assert.True(TypedKeysFormatter.Format(Text("kyouha"), TypedKeysStyle.None) is null, "表示しない");
        Assert.True(TypedKeysFormatter.Format(Text(""), TypedKeysStyle.Hiragana) is null, "何も打っていない");
    }

    [Test]
    public static void Hiragana_KeepsEnglishAndPendingConsonant()
    {
        Assert.Equal("きょうはgoogle", TypedKeysFormatter.Format(Text("kyouhagoogle"), TypedKeysStyle.Hiragana));
        Assert.Equal("きょうはk", TypedKeysFormatter.Format(Text("kyouhak"), TypedKeysStyle.Hiragana), "入力途中の子音は子音のまま");
    }

    [Test]
    public static void HiraganaSpaced_SplitsWordsAndEnglish()
    {
        Assert.Equal("きょう は google", TypedKeysFormatter.Format(Text("kyouhagoogle"), TypedKeysStyle.HiraganaSpaced, Words));
        Assert.Equal("きょう は いい てんき です", TypedKeysFormatter.Format(Text("kyouhaiitenkidesu"), TypedKeysStyle.HiraganaSpaced, Words));
        Assert.Equal("きょう はk", TypedKeysFormatter.Format(Text("kyouhak"), TypedKeysStyle.HiraganaSpaced, Words), "入力途中の子音は解析に渡さず最後の単語に付ける");
    }

    [Test]
    public static void HiraganaSpaced_FailsOpenToUnsplit()
    {
        // 解析できない・区切りが読みと合わない・例外 → 区切らずに出す (打ったキーは欠かさない)
        Assert.Equal("きょうは google", TypedKeysFormatter.Format(Text("kyouhagoogle"), TypedKeysStyle.HiraganaSpaced, null));
        Assert.Equal("きょうは google", TypedKeysFormatter.Format(Text("kyouhagoogle"), TypedKeysStyle.HiraganaSpaced, _ => ["きょう", "が"]));
        Assert.Equal("きょうは google", TypedKeysFormatter.Format(Text("kyouhagoogle"), TypedKeysStyle.HiraganaSpaced, _ => ["きょうは", ""]));
        Assert.Equal("きょうは google", TypedKeysFormatter.Format(Text("kyouhagoogle"), TypedKeysStyle.HiraganaSpaced, _ => throw new InvalidOperationException()));
    }

    [Test]
    public static void Session_PassesFormattedTypedKeys()
    {
        // 変換ボックス (Meltype キーボード) とパイプの JSON (Meltype IME) は同じ CompositionView.Typed を使う
        var session = new MeltypeSession(CompositionTests.Detector, new CompositionTests.FakeConverter(),
            new CompositionOptions { ShowTypedKeys = () => true, TypedKeysFormat = () => TypedKeysStyle.HiraganaSpaced, TypedKeysWords = Words }, () => new Settings());
        SessionResult? last = null;
        foreach (var c in "kyouha") last = session.HandleKey(char.ToUpperInvariant(c), c, false, false, false, false, null);
        Assert.True(last!.ToJson().Contains("\"typed\":\"きょう は\""), last.ToJson());
        last = session.HandleKey(VirtualKeys.Space, null, false, false, false, false, null);
        Assert.True(last.View!.Converting, "変換中");
        Assert.Equal("きょう は", last.View.Typed, "変換中も出す");

        var off = new MeltypeSession(CompositionTests.Detector, new CompositionTests.FakeConverter(),
            new CompositionOptions { ShowTypedKeys = () => false, TypedKeysFormat = () => TypedKeysStyle.Hiragana }, () => new Settings());
        foreach (var c in "kyouha") last = off.HandleKey(char.ToUpperInvariant(c), c, false, false, false, false, null);
        Assert.True(last.View!.Typed is null, "ShowTypedKeys が OFF なら形式にかかわらず出さない");
    }

    [Test]
    public static void Settings_DisplayBridgesShowTypedKeys()
    {
        Assert.Equal(TypedKeysStyle.Romaji, new Settings().TypedKeysDisplay, "既定は今までどおりローマ字");
        Assert.Equal(TypedKeysStyle.None, new Settings { ShowTypedKeys = false }.TypedKeysDisplay, "今までの OFF は「表示しない」");

        var settings = new Settings { TypedKeysDisplay = TypedKeysStyle.HiraganaSpaced };
        Assert.True(settings.ShowTypedKeys, "形式を選ぶと ON");
        Assert.Equal(TypedKeysStyle.HiraganaSpaced, settings.TypedKeysFormat);
        settings.TypedKeysDisplay = TypedKeysStyle.None;
        Assert.True(!settings.ShowTypedKeys, "表示しないは OFF");
        Assert.Equal(TypedKeysStyle.HiraganaSpaced, settings.TypedKeysFormat, "形式は残す");
    }

    [Test]
    public static void Settings_SaveLoadKeepsFormat()
    {
        var directory = Path.Combine(Path.GetTempPath(), "meltkelp-typed-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "config.json");
            new Settings { TypedKeysDisplay = TypedKeysStyle.Hiragana }.Normalize().Save(path);
            Assert.True(File.ReadAllText(path).Contains("\"TypedKeysFormat\": \"Hiragana\""), "形式は名前で書く");
            Assert.True(!File.ReadAllText(path).Contains("TypedKeysDisplay"), "設定画面用の値は書かない");
            Assert.Equal(TypedKeysStyle.Hiragana, Settings.Load(path).TypedKeysDisplay);

            // MeltKelp #27 より前の config.json (形式のキーが無い) は、ON ならローマ字・OFF なら表示しない
            File.WriteAllText(path, """{ "ShowTypedKeys": false }""");
            Assert.Equal(TypedKeysStyle.None, Settings.Load(path).TypedKeysDisplay);
            File.WriteAllText(path, """{ "ShowTypedKeys": true }""");
            Assert.Equal(TypedKeysStyle.Romaji, Settings.Load(path).TypedKeysDisplay);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
