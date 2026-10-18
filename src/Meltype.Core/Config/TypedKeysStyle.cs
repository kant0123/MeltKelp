// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

using System.ComponentModel;

namespace Meltype.Config;

/// <summary>
/// MeltKelp: 打ったキーを表示するときの形式 (設定「打ったキーを表示」。MeltKelp #27)。並びは設定画面のプルダウンの並び。
/// </summary>
public enum TypedKeysStyle
{
    /// <summary>打ったキーそのまま (kyouhagoogle)。上流と同じ。</summary>
    [Description("ローマ字")] Romaji,
    /// <summary>出さない。config.json では上流の ShowTypedKeys = false で持つ。</summary>
    [Description("表示しない")] None,
    /// <summary>読みのひらがな (きょうはgoogle)。英語と判定した区間は打ったまま。</summary>
    [Description("ひらがな")] Hiragana,
    /// <summary>読みのひらがなを単語 (形態素) ごとに空白で区切る (きょう は google)。</summary>
    [Description("ひらがな (単語ごとに空白)")] HiraganaSpaced,
}
