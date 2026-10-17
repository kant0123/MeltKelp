// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

namespace Meltype.Tests;

/// <summary>MeltKelp の版 (Config.MeltKelpInfo) を、上流の版とは別に出す。</summary>
internal static class MeltKelpVersionTests
{
    [Test]
    public static void MeltKelpVersion_IsShownBesideUpstreamVersion()
    {
        Assert.True(Version.TryParse(Config.MeltKelpInfo.Version, out _), $"版の形: {Config.MeltKelpInfo.Version}");
        Assert.True(AppInfo.AboutText.Contains($"MeltKelp {Config.MeltKelpInfo.Version} (Meltype {AppInfo.Version} ベース)"), AppInfo.AboutText);
        Assert.True(AppInfo.IsPublicRelease, "上流の版は変えない (公開版の扱いのまま)");
    }
}
