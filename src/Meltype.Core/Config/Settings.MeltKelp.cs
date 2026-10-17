// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。上流の Settings.cs への変更は partial の 1 語だけにする。

using System.ComponentModel;

namespace Meltype.Config;

public sealed partial class Settings
{
    [Category("1. 全般"), DisplayName("zenz で候補を文脈に合わせて並べ替える (MeltKelp)"),
     Description("漢字変換の最初の候補を、キャレットの前の文字と同じ変換の前の文節を文脈にして、言語モデル zenz で選び直します (劇団の千秋楽の + こうえん → 公演)。ランタイム (llama.dll とモデル) を %LOCALAPPDATA%\\Meltype\\zenz に置いたときだけ働き、無ければ OFF と同じです。選び直した変換の学習・ユーザー辞書の語は、zenz より優先します。")]
    public bool ZenzRerank { get; set; } = true;
}
