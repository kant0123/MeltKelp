---
type: concept
summary: 上流 Meltype のリリースによる自動更新(確認・ダウンロード・適用・手動)を MeltKelp で止めた理由と仕組み。上流の取り込みで Updater.cs が衝突したときに読む
updated: 2026-10-17
related: [overview, build-install]
---

# 上流のリリースによる更新を止める

MeltKelp は、上流 yksr-melt/Meltype の GitHub のリリースによる更新を、確認・ダウンロード・起動時の適用・
トレイからの手動の更新のすべてで止めている(#3)。設定の「自動で更新する」(`Settings.AutoUpdate`)が ON のままでも止まる。

## 内容

判定は 1 か所で、`src/Meltype/UpstreamUpdate.cs:18 UpstreamUpdate.Allowed`(常に false)。上流のファイルには、
これを見る 1 行を 4 か所に差し込んだだけ(行末に `MeltKelp #3` のコメント)。

| 差し込み口 | 止めるもの |
| --- | --- |
| `src/Meltype/Updater.cs:41 Staged()` が null を返す | ダウンロード済みの版(`%LOCALAPPDATA%\Meltype\update\ready.txt`)を対象にしない。`ApplyStagedAtStartup()`(サインイン時の適用)・`Apply()`(トレイの「更新して再起動」・通知のクリック)・`PreviewChanges()`・トレイの「更新して再起動」の項目が、どれもここを通るので一緒に止まる |
| `src/Meltype/Updater.cs:171 Run()` が `update.ps1` を動かさずに `UpstreamUpdate.Message` を返す | 起動の 2 分後と 6 時間ごとの確認(`Check()`)と、トレイの「今すぐ更新を確認」。何もダウンロードせず、ログにも何も書かない |
| `src/Meltype/UI/TrayApplicationContext.cs:139` の `_autoUpdateItem.Visible` | トレイの「更新」→「自動で更新する」を出さない。「今すぐ更新を確認」は残し、押すと止めている旨を出す |
| `src/Meltype/UI/SettingsForm.cs:291 BuildGroups()` の `UpstreamUpdate.ShowsSetting` | 設定画面の「9. 更新」→「自動で更新する」を出さない(「9. 更新」の項目はこれだけなので、枠ごと消える) |

- `settings.json` の `AutoUpdate` の値は読み書きしない。公式版から置き換えても、値は残ったまま使われない
- 既にダウンロードされていた `update\` のフォルダーは消さない(読まないだけ)。要らなければ手で消してよい
- テストは `src/Meltype.Tests/UpstreamUpdateTests.cs`。`StagedUpdate_IsNotApplied()` が、実データとは別のデータフォルダー
  (`MELTYPE_DATA_DIR`)に今より新しい版の `ready.txt` と `install.ps1` を置き、この実行ファイルを子プロセスで起動して、
  起動時の適用・`Apply()` が動かないことを確かめる。データフォルダーはプロセスの起動時に 1 回だけ決まる
  (`AppPaths.DataDirectory`)ので、子プロセスに分けている。`Allowed` を true にするとこのテストを含め 3 件が落ちることを確かめてある

## なぜこうなっているか

- MeltKelp は公式と同じ場所(`%LOCALAPPDATA%\Programs\Meltype`)に入れて公式を置き換える([[overview]])。上流の版で更新されると、
  zenz の並べ替え(#2)などの MeltKelp の変更が黙って消える
- 上流の `Updater.cs` が見るのは `AppInfo.IsPublicRelease`(版が 1.0.0 以上)だけで、MeltKelp は上流の版(`Meltype.csproj` の
  `<Version>`)をそのまま持つので「公開版」と判定され、確認が走る。しかも `ApplyStagedAtStartup()` と `Apply()` はこの判定も見ない
- `AutoUpdate` は既定が ON で、設定が初期化されると戻る。OFF でもトレイの「今すぐ更新を確認」から上流の版を入れられた

## 検討して採らなかった選択肢

| 案 | 却下した理由 |
| --- | --- |
| 版を 1.0.0 未満に下げて `IsPublicRelease` を偽にする | 版は上流に合わせる方針(`upstream-sync` スキル)。`IsPublicRelease` は不具合報告の画面にも効く。ダウンロード済みの版の適用(`ApplyStagedAtStartup()`)はこの判定を見ないので止まらない |
| `Settings.AutoUpdate` の既定を OFF にする | `Meltype.Core` の上流のファイルを変える。手動の確認は止まらず、ON に戻せばまた入る |
| `AppInfo.UpdateRepository` を `kant0123/MeltKelp` に向ける | private なので確認は 404 で失敗するだけ。MeltKelp はリリースを作らない |
| `packaging/update.ps1`・`install.ps1` を変える | ダウンロード済みの版の `install.ps1` は上流のものなので、こちらを変えても適用は止まらない |
| `#if` のビルド定数で切り替える | MeltKelp のビルドはすべて MeltKelp なので、切り替える相手が無い。常に false の判定を 1 か所に置けば足りる |
| トレイの「更新」メニューごと消す | 差し込みは 1 行で済むが、「今すぐ更新を確認」を残して止めている旨を出す方が、なぜ更新されないのかが分かる |

## 上流の取り込みで衝突したら

- 4 か所の差し込みはどれも 1 行。上流がその周りを変えたら、同じ役割の場所(ダウンロード済みの版を返す入口・
  `update.ps1` を動かす入口・自動更新の切り替えの項目・設定画面の項目の並び)に差し直す
- **上流が新しい更新の経路を足したら、`Staged()` か `Run()` を通るかを確かめる。** 通らないなら、その経路にも差し込む。
  `UpstreamUpdateTests` が子プロセスで `ApplyStagedAtStartup()` と `Apply()` を試すので、経路が `Staged()` を迂回すると落ちる
- 版の表記・`IsPublicRelease` を上流が変えても、この仕組みは版を見ないので影響しない

## 覆すとしたら

MeltKelp 自身の版を配って自動で更新したくなったら(今は配布しない)、`UpstreamUpdate.Allowed` を戻すのではなく、
`AppInfo.UpdateRepository` と版の付け方から考え直す。上流の版をそのまま持つ限り、上流のリリースを「新しい版」と判定してしまう。

## 関連

- [[overview]] — 公式を置き換える前提と、上流との差分を最小にする方針
- [[build-install]] — 入れ直しと実機での確認の手順
