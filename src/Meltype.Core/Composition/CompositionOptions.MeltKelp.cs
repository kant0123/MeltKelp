// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。上流の CompositionController.cs への変更は partial の 1 語だけにする。

namespace Meltype.Composition;

public sealed partial record CompositionOptions
{
    /// <summary>MeltKelp: zenz で候補を文脈つきに並べ替えるか (設定の ZenzRerank。MeltKelp #2)。</summary>
    public Func<bool> ZenzRerank { get; init; } = () => false;
}
