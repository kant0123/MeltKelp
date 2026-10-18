// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

namespace Meltype.Config;

/// <summary>
/// MeltKelp の版。上流の版 (Meltype.csproj の Version。今は 1.2.1) とは別に持つ。
/// 上流の版を変えないのは、IsPublicRelease (メジャー 1 以上) で公開版かを決めていて、0.x にするとプレビュー版の扱いに変わるのと、
/// 上流の取り込みのたびに衝突するため。リリースのタグは meltkelp-v&lt;版&gt; (v* は上流の build.yml がリリースを作ろうとする)。
/// </summary>
public static class MeltKelpInfo
{
    public const string Name = "MeltKelp";

    /// <summary>0.1.0: zenz で漢字変換の候補を文脈つきに並べ替える (MeltKelp #2)。</summary>
    public const string Version = "0.1.0";

    /// <summary>画面・--version に出す 1 行 (MeltKelp 0.1.0 (Meltype 1.2.1 ベース))。</summary>
    public static string Title(string upstreamVersion) => $"{Name} {Version} (Meltype {upstreamVersion} ベース)";
}
