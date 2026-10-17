// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

using System.Diagnostics;

namespace Meltype.Tests;

/// <summary>MeltKelp: 上流のリリースによる更新を止めていること (MeltKelp #3)。</summary>
internal static class UpstreamUpdateTests
{
    /// <summary>子プロセスでだけ <see cref="StagedProbe"/> を動かす印 (値は ready.txt を置いたデータフォルダー)。</summary>
    private const string ProbeVariable = "MELTKELP_STAGED_PROBE";

    [Test]
    public static void CheckNow_DoesNotLookAtUpstream()
    {
        // 上流の版 (1.0.0 以上) をそのまま持つので公開版と判定される。この判定では止まらないので、UpstreamUpdate で止める
        Assert.True(AppInfo.IsPublicRelease, $"MeltKelp の版 {AppInfo.Version} は公開版と判定される");
        Assert.True(!UpstreamUpdate.Allowed, "上流の更新は止めている");
        // 自動更新が ON でも、update.ps1 を動かさずに止めている旨を返す
        using var updater = new Updater(() => true);
        Assert.Equal(UpstreamUpdate.Message, updater.CheckNowAsync().Result, "今すぐ確認しても上流を見に行かない");
    }

    [Test]
    public static void Settings_HideAutoUpdate()
    {
        var properties = typeof(Config.Settings).GetProperties().Where(UpstreamUpdate.ShowsSetting).Select(p => p.Name).ToList();
        Assert.True(!properties.Contains(nameof(Config.Settings.AutoUpdate)), "設定画面に「自動で更新する」を出さない");
        Assert.True(properties.Contains(nameof(Config.Settings.ShowNotifications)), "ほかの項目は出す");
    }

    [Test]
    public static void StagedUpdate_IsNotApplied()
    {
        // ダウンロード済みの上流の版 (今より新しい版と install.ps1) を、実データとは別のデータフォルダーに置き、
        // そのフォルダーを使う子プロセス (この実行ファイル) で起動時の適用を試す。
        // データフォルダーは起動時に 1 回だけ決まる (AppPaths.DataDirectory) ので、このプロセスでは差し替えられない
        var data = Path.Combine(Path.GetTempPath(), $"meltkelp-staged-{Environment.ProcessId}");
        var staged = Path.Combine(data, "update", "99.0.0");
        try
        {
            Directory.CreateDirectory(staged);
            var marker = Path.Combine(data, "installed.txt");
            File.WriteAllText(Path.Combine(staged, "install.ps1"), $"Set-Content -LiteralPath '{marker}' -Value installed");
            File.WriteAllText(Path.Combine(data, "update", "ready.txt"), $"99.0.0\t{staged}");

            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            start.ArgumentList.Add($"{nameof(UpstreamUpdateTests)}.{nameof(StagedProbe)}");
            start.Environment["MELTYPE_DATA_DIR"] = data;
            start.Environment[ProbeVariable] = data;
            using var process = Process.Start(start)!;
            var output = Updater.ReadOutput(process, 120_000);
            Assert.True(output is not null, "子プロセスが時間内に終わる");
            Assert.True(process.ExitCode == 0 && output!.Contains("1/1 passed"), $"子プロセスで適用しない: {output}");
            // 適用していれば install.ps1 が非同期に動くので、少し待ってから見る
            Thread.Sleep(2000);
            Assert.True(!File.Exists(marker), "install.ps1 を動かさない");
        }
        finally
        {
            try { Directory.Delete(data, recursive: true); } catch { }
        }
    }

    /// <summary><see cref="StagedUpdate_IsNotApplied"/> が起動した子プロセスで動く (ふだんのテストの実行では何もしない)。</summary>
    [Test]
    public static void StagedProbe()
    {
        if (Environment.GetEnvironmentVariable(ProbeVariable) is not { Length: > 0 } data) return;
        Assert.Equal(data, Config.AppPaths.DataDirectory, "子プロセスのデータフォルダー");
        Assert.True(File.Exists(Path.Combine(data, "update", "ready.txt")), "ダウンロード済みの更新がある");
        Assert.True(Updater.Staged() is null, "ダウンロード済みの上流の版を更新の対象にしない");
        var confirmed = false;
        Assert.True(!Updater.ApplyStagedAtStartup(() => true, _ => confirmed = true), "自動更新が ON でも起動時に適用しない");
        Assert.True(!confirmed, "変わる語の確認も出さない");
        Assert.True(!Updater.Apply(), "トレイの「更新して再起動」でも適用しない");
    }
}
