# Log

Wiki に対して何を・いつ行ったかの追記専用ログ。**新しいエントリは末尾に足す**(既存行は書き換えない)。

各エントリは `## [YYYY-MM-DD] <種別> | <一行要約>` で始める。種別は以下の 3 つ。

| 種別 | 意味 |
| --- | --- |
| `ingest` | 実装・変更の内容を Wiki に反映した |
| `query` | 調査・分析の結果を Wiki のページとして還元した |
| `lint` | 健康診断を行い、矛盾・陳腐化・孤立ページを修正した |

直近の動きは次のコマンドで追える。

```bash
grep "^## \[" wiki/log.md | tail -5
```

---

<!-- 記入例(実際のエントリを追記したらこのコメントは削除してよい)

## [2026-08-11] ingest | PR #123 エピソード進捗の一括更新に対応
- 更新: [[episode-progress]], [[db-naming]]
- 新規: [[bulk-update-transaction]]
- 備考: 一括更新はトランザクション境界が従来と異なる。[[batch-updater]] からの相互リンクを追加。

## [2026-08-12] lint | 孤立ページと陳腐化した記述の整理
- 更新: [[index]], [[deploy]]
- 削除: なし
- 発見: [[legacy-import]] は被リンク 0。実装も削除済みのためページを削除した。

-->

## [2026-10-17] ingest | PR #4 エージェント規約のテンプレートを適用し、MeltKelp の Wiki を立ち上げる
- 新規: [[overview]], [[engine-base-choice]], [[context-rerank-lessons]], [[build-install]]
- 更新: [[recurring-review-findings]]
- 備考: #1。テンプレート(kant0123/ai-coding-template-mod-gemini-review)の log にあったテンプレート自身のエントリは、
  MeltKelp の履歴ではないので持ち込んでいない。imev(kant0123/imev)の wiki から、コードを読んでも分からない知見だけを
  書き直して移した(出典は各ページ)。zenz の並べ替え(#2)と自動更新の停止(#3)は未実装。
  main のブランチ保護は private の無料プランで 403 になり、掛けられていない([[overview]] の「運用」)。

## [2026-10-17] ingest | PR #7 main のブランチ保護を掛けない運用にした判断を残す
- 更新: [[overview]]
- 備考: #5。PR #4 で保護の適用が 403(private の無料プラン)になり、ユーザーが Pro にも public にもせず保護なしで運用すると決めた。`docs/development_workflow.md` の「ブランチ保護」節も合わせた。作業中に merge hook の誤検知を見つけて #6 に起票した。

## [2026-10-17] ingest | PR #9 上流のリリースによる自動更新を止める
- 新規: [[upstream-update-stop]]
- 更新: [[overview]], [[build-install]](実施記録に実機での確認を追記)
- 備考: #3。判定は `src/Meltype/UpstreamUpdate.cs` に置き、上流のファイルには 1 行ずつ 4 か所差し込んだ。
  手元の .NET SDK が 9 だったので、winget で .NET 10 SDK(10.0.402)を入れてから `Meltype.Tests` を流した。

## [2026-10-17] ingest | PR #11 テンプレートの更新(コード参照の解釈・hooks.json の形・.agent/rules の写し)を取り込む
- 更新: [[recurring-review-findings]](テンプレート PR #47 の実例と、種類「外部ツールの設定形式を一次資料で確かめずに書く」。agy レビューで妥当だった `deploy.yml` の指摘も同じ種類に足した)
- 備考: #10。PR #4 で適用したテンプレートは PR #42 までの版だった。テンプレート PR #47・#48 のうち、`README.md`・`selfcheck.yml`・`deploy/`・
  `scripts/setup.*`(PR #4 で持ち込んでいないもの)以外を取り込んだ。コード参照はルート直下のファイル(`CLAUDE.md` など)を指すものも
  書けるようになった(スラッシュも記号名も無いものは、ファイルが実在するときだけ参照として数える)。
  `.agent/rules/` は MeltKelp の `CLAUDE.md` の写しとして書き直した(フォークの不変条件・実機確認・agy レビュー必須を含む)。

## [2026-10-18] ingest | PR #12 報告の導線を上流に向けない
- 新規: [[upstream-report-stop]]
- 更新: [[overview]], [[upstream-update-stop]]
- 備考: #8。判定は `src/Meltype/UpstreamReport.cs` に置き、`AppInfo` の報告の URL 3 つ・トレイの「不具合の報告・提案...」
  「直した誤判定を報告...」に 1 行ずつ差し込んだ。「不具合の報告・提案...」は当初残していたが、ユーザーの判断で消した。

## [2026-10-18] ingest | PR #15 agy レビューの merge hook が --repo / -R を見て別リポジトリの PR を判定する
- 更新: [[overview]]
- 備考: #14。hook は PR 番号だけを cwd のリポジトリの PR として引いていた。`--repo` / `-R` を `agy_review.py --check --repo` に渡し、
  そのリポジトリの PR のレビュー記録を見る。別リポジトリでも判定は外さない。`review/README.md` にも説明を足した。
  マージしないコマンドの文字列に反応する件は #6 のまま(未対応)。テンプレート側の hook も同じ作り。

## [2026-10-18] ingest | PR #17 クリップボードのテストが時々落ちるのを直す
- 更新: [[build-install]]
- 備考: #13。上流のテスト(`WindowsTests.cs`)の待ち方だけを変えた。落ちる頻度と原因(`cbdhsvc` が掴む・`Thread.Sleep` で止まる)は
  [[build-install]] の落とし穴に書いた。
