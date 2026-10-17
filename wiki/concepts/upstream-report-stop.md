---
type: concept
summary: トレイの「不具合の報告・提案」「直した誤判定を報告」をトレイから消し、報告の URL が上流 Meltype の Issue 画面を開かないようにした理由と仕組み。上流の取り込みで AppInfo.cs・TrayApplicationContext.cs が衝突したときに読む
updated: 2026-10-18
related: [overview, upstream-update-stop]
---

# 上流への報告の導線を止める

MeltKelp は、上流のままだと上流 yksr-melt/Meltype の Issue の作成画面を開く報告の導線を、すべて上流に向けないようにしている(#8)。

## 内容

判定は `src/Meltype/UpstreamReport.cs:16 Allowed`(`UpstreamReport.Allowed`、常に false)。[[upstream-update-stop]] と同じ形で、
上流のファイルには行末に `MeltKelp #8` のコメントを付けた差し込みだけを入れた。

| 差し込み口 | 止めるもの |
| --- | --- |
| `src/Meltype/AppInfo.cs:17 GitHubReportUrl()`・`:23 MisdetectionReportUrl()`・`:28 ReportUrl()` | 報告の URL を、上流の Issue の画面ではなく `UpstreamReport.IssueUrl`(`kant0123/MeltKelp` の Issue の作成画面、クエリ無し)にする。報告の画面を開く 4 か所(`ReportDialog` の「フォームで報告」と GitHub のリンク・`CorrectionReportDialog`・`UpdateChangesDialog` の「報告する...」)がどれもここを通るので、入口を隠し損ねても上流には行かない。トレイから消した後もここを止めているのは、`UpdateChangesDialog` と、上流が今後足す入口のため |
| `src/Meltype/UI/TrayApplicationContext.cs:43 TrayApplicationContext()` の「不具合の報告・提案...」「直した誤判定を報告...」の `Visible` | トレイにこの 2 項目を出さない。`ReportDialog`・`CorrectionReportDialog` はトレイからしか開かれないので、画面ごと使われなくなる |

- 2 項目とも消したのはユーザーの判断(#8)。MeltKelp の Issue は開発者本人が立てるので、報告の画面を経由する必要が無い。
  最近のログはトレイの「ログ / 判定理由...」・データフォルダーの `meltype.log` で見られる
- 上流の `src/Meltype.Core/Config/ProjectInfo.cs`(Mac 版・Linux 版と共通)は変えていない。`ProjectInfo.SourceUrl` は上流のまま
- 「Meltype について」のソースコードの URL(`AppInfo.AboutText`)も上流のまま。MeltKelp は配布しないので、GPL の表示は上流の版のもので足りる
- テストは `src/Meltype.Tests/UpstreamReportTests.cs`。`AppInfo` の 3 つの URL が上流を含まず `IssueUrl` そのものであることを確かめる

## なぜこうなっているか

- MeltKelp は上流に何も出さない([[overview]])。上流のままだとトレイの項目が上流の Issue の画面を開き、MeltKelp の不具合を上流に報告しかねない
- 上流の `ReportDialog` は `AppInfo.IsPublicRelease`(版 1.0.0 以上)で上流の GitHub のリンクを出す。MeltKelp は上流の版をそのまま持つので出てしまう
- 誤判定の報告の URL(`ProjectInfo.MisdetectionReportUrl()`)には、打った文字列・出た候補・前後の文がクエリとして入る。
  開くだけでブラウザーの履歴に入力内容が残り、送れば Issue に入る(入力内容は個人情報として扱う方針)

## 検討して採らなかった選択肢

| 案 | 却下した理由 |
| --- | --- |
| 報告の URL を `kant0123/MeltKelp` の Issue のひな形に、クエリつきのまま向ける | 誤判定の報告で入力内容が URL と Issue に入る。MeltKelp に上流の Issue のひな形(`.github/ISSUE_TEMPLATE`)はあるが、MeltKelp の Issue は開発者本人が立てるので、欄を自動で埋める必要が無い |
| `ProjectInfo.SourceUrl` を MeltKelp に向ける | `Meltype.Core` の上流のファイルを変える。「Meltype について」のソースコードの表示まで変わる |
| 「不具合の報告・提案...」は残し、URL を MeltKelp に向けて GitHub のリンクだけ隠す | 最初はこうしていた(ログをコピーする画面として使える)が、ユーザーが項目ごと消してよいと判断した。上流のファイルへの差し込みも 1 か所減る |
| URL の差し込みをせず、トレイの入口だけを隠す | `UpdateChangesDialog` の「報告する...」はトレイを通らない(更新を止めているので今は出ないが、[[upstream-update-stop]] を外すと出る)。上流が足した入口を見落とすと上流に向く。URL の 3 か所で止めれば入口が増えても上流には行かない |

## 上流の取り込みで衝突したら

- `AppInfo.cs` の 3 つの差し込みは、各メソッドの先頭の条件 1 行。上流が URL のメソッドを足したら、同じ 1 行を差し込む
- **上流が `AppInfo` を通らずに `ProjectInfo` の URL を直接開く入口を足したら、そこにも差し込む。** `UpstreamReportTests` は `AppInfo` しか見ないので、この迂回は検知できない。取り込みの後に `ProjectInfo.SourceUrl` と `ProjectInfo.*ReportUrl` の使用箇所を grep して確かめる

## 関連

- [[overview]] — 上流に何も出さない方針
- [[upstream-update-stop]] — 同じ形で上流のリリースによる更新を止めている
