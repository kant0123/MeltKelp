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
- 更新: [[overview]], [[build-install]]
- 備考: #3。判定は `src/Meltype/UpstreamUpdate.cs` に置き、上流のファイルには 1 行ずつ 4 か所差し込んだ。
  手元の .NET SDK が 9 だったので、winget で .NET 10 SDK(10.0.402)を入れてから `Meltype.Tests` を流した。
