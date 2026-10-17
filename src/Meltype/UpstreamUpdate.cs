// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

using System.Reflection;

namespace Meltype;

/// <summary>
/// MeltKelp: 上流 (<see cref="AppInfo.UpdateRepository"/>) のリリースによる更新を止める (MeltKelp #3)。
/// MeltKelp は公式と同じ場所 (%LOCALAPPDATA%\Programs\Meltype) に入れて公式を置き換えるので、上流の版で更新すると
/// MeltKelp の変更が黙って消える。上流の版 (Meltype.csproj の Version) をそのまま持つので <see cref="AppInfo.IsPublicRelease"/> は真になり、
/// 設定の AutoUpdate も既定で ON なので、どちらでも止まらない。
/// 差し込み口は Updater.Staged・Updater.Run・TrayApplicationContext (「自動で更新する」)・SettingsForm (「9. 更新」) の各 1 行。
/// </summary>
internal static class UpstreamUpdate
{
    /// <summary>上流のリリースで更新してよいか。MeltKelp では常に false (設定の AutoUpdate が ON でも)。</summary>
    public static bool Allowed => false;

    /// <summary>トレイの「今すぐ更新を確認」で見せる文言。</summary>
    public const string Message = "MeltKelp では、上流の Meltype (" + AppInfo.UpdateRepository + ") のリリースによる更新を止めています (公式版で上書きされ、MeltKelp の変更が消えるため)。新しい版はソースからビルドして入れ直してください。";

    /// <summary>設定画面に出す項目か。更新を止めている間は「自動で更新する」を出さない (切り替えても何も変わらないため)。</summary>
    public static bool ShowsSetting(PropertyInfo property) => Allowed || property.Name != nameof(Config.Settings.AutoUpdate);
}
