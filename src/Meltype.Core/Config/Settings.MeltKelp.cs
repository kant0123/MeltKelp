// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。上流の Settings.cs への変更は partial の 1 語だけにする。

using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Meltype.Config;

public sealed partial class Settings
{
    [Category("1. 全般"), DisplayName("zenz で候補を文脈に合わせて並べ替える (MeltKelp)"),
     Description("漢字変換の最初の候補を、キャレットの前の文字と同じ変換の前の文節を文脈にして、言語モデル zenz で選び直します (劇団の千秋楽の + こうえん → 公演)。ランタイム (llama.dll とモデル) を %LOCALAPPDATA%\\Meltype\\zenz に置いたときだけ働き、無ければ OFF と同じです。選び直した変換の学習・ユーザー辞書の語は、zenz より優先します。")]
    public bool ZenzRerank { get; set; } = true;

    [Category("1. 全般"), DisplayName("zenz を GPU で動かす (MeltKelp)"),
     Description("ON にすると、zenz のランタイムの GPU 版 (%LOCALAPPDATA%\\Meltype\\zenz\\vulkan) があれば GPU で採点し、打鍵の遅れが減ります (無ければ CPU 版)。OFF にすると CPU 版 (zenz\\cpu) だけを使い、GPU には触りません (CPU 版が無ければ並べ替えをしません)。切り替えは Meltype を起動し直すと効きます。")]
    public bool ZenzUseGpu { get; set; } = true;

    [Category("1. 全般"), DisplayName("打ったキーを表示"),
     Description("打ったキーを小さく出します。Meltype キーボードでは変換ボックスの文字の下に、Meltype IME では候補の一覧の上に出します。打ち間違いに気づきやすくなります。ローマ字: kyouhagoogle / ひらがな: きょうはgoogle / ひらがな (単語ごとに空白): きょう は google (英語と判定した部分は打ったまま。単語の区切りは Microsoft IME で調べ、調べられないときは空白なし)。ひらがなは、変換ボックスの文字と同じときは出しません。")]
    [JsonIgnore]
    public TypedKeysStyle TypedKeysDisplay
    {
        get => ShowTypedKeys ? TypedKeysFormat : TypedKeysStyle.None;
        set
        {
            ShowTypedKeys = value != TypedKeysStyle.None;
            if (value != TypedKeysStyle.None) TypedKeysFormat = value;
        }
    }

    /// <summary>MeltKelp #27: 打ったキーを出すときの形式 (出さないときも、次に出すときのために残す)。</summary>
    [Browsable(false)]
    public TypedKeysStyle TypedKeysFormat { get; set; } = TypedKeysStyle.Romaji;
}
