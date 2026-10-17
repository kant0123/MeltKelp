// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

namespace Meltype;

/// <summary>
/// MeltKelp: 不具合・誤判定の報告を上流 (<see cref="Config.ProjectInfo.SourceUrl"/>) に向けない (MeltKelp #8)。
/// MeltKelp は上流に何も出さない。上流のままだと、トレイの報告の項目が上流の Issue の作成画面を開き、
/// 誤判定の報告では打った文字列・前後の文が URL のクエリに入る。
/// 差し込み口は AppInfo の報告の URL 3 つ・TrayApplicationContext (「不具合の報告・提案...」「直した誤判定を報告...」) の各 1 行。
/// 「Meltype について」のソースコードの URL は上流のまま (MeltKelp は配布しないので、GPL の表示は上流の版のもので足りる)。
/// </summary>
internal static class UpstreamReport
{
    /// <summary>上流に報告してよいか。MeltKelp では常に false。</summary>
    public static bool Allowed => false;

    /// <summary>
    /// 報告の URL の代わりに開く、MeltKelp の Issue の作成画面。クエリは付けない
    /// (入力した文字・実行環境を URL に入れない。ブラウザーの履歴に残り、Issue に入力内容を書かない方針に反するため)。
    /// </summary>
    public const string IssueUrl = "https://github.com/kant0123/MeltKelp/issues/new";
}
