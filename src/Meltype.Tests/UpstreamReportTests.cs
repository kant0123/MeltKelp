// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

namespace Meltype.Tests;

/// <summary>MeltKelp: 不具合・誤判定の報告を上流に向けないこと (MeltKelp #8)。</summary>
internal static class UpstreamReportTests
{
    [Test]
    public static void ReportUrls_DoNotPointToUpstream()
    {
        // 上流の版 (1.0.0 以上) をそのまま持つので、上流のままなら GitHub の報告のリンクも出る
        Assert.True(AppInfo.IsPublicRelease, $"MeltKelp の版 {AppInfo.Version} は公開版と判定される");
        Assert.True(!UpstreamReport.Allowed, "上流には報告しない");

        var correction = new Composition.Correction("kousinn", "こうしん", "更新", "Space (変換)", "設定の", DateTime.Now);
        var urls = new[]
        {
            AppInfo.ReportUrl("env"),
            AppInfo.GitHubReportUrl("1-bug.yml", "env"),
            AppInfo.GitHubReportUrl("2-misdetection.yml", "env"),
            AppInfo.MisdetectionReportUrl(correction, "env"),
        };
        foreach (var url in urls)
        {
            Assert.True(!url.Contains(AppInfo.UpdateRepository, StringComparison.OrdinalIgnoreCase), "上流を開かない: " + url);
            Assert.Equal(UpstreamReport.IssueUrl, url, "MeltKelp の Issue の作成画面を、何も入れずに開く");
        }
        // 上流の ProjectInfo (Mac 版・Linux 版と共通) は変えていない
        Assert.True(Config.ProjectInfo.SourceUrl.Contains(AppInfo.UpdateRepository), "ProjectInfo は上流のまま");
    }
}
