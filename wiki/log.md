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
- 更新: [[overview]], [[recurring-review-findings]]
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

## [2026-10-18] ingest | PR #19 README を MeltKelp 向けに書き直す
- 更新: [[overview]]
- 備考: #18。README が上流のまま(上流のリリース・Discord・Sponsors・協力者・報告の案内)で、MeltKelp の実態と食い違っていた。
  何であるか・上流との関係・ビルドして入れる手順(詳細は [[build-install]])・開発の入口に置き換え、zenz の並べ替えが未実装(#2)であることを表に書いた。
  上流の著作権表示と GPL-3.0 は残した。上流の取り込みで README は衝突するので、MeltKelp 側を残す。

## [2026-10-18] ingest | PR #21 README から画像を外す
- 備考: #20。ユーザーの判断で、README のロゴ・デモ・バッジをすべて外した(`docs/images/` のファイルは上流の他の文書が使うので残す)。
  ロゴの作者の謝辞は文章に残してある。Wiki のページへの影響なし。

## [2026-10-18] ingest | PR #16 merge hook が引数の中の文字列に反応しないようにする
- 更新: [[overview]], [[recurring-review-findings]]
- 備考: #6。判定を「コマンド文字列に含まれるか」から「実行されるコマンドか」に変えた(`.claude/hooks/command_match.mjs`)。
  `check_agy_review.sh` と `check_wiki_updated.sh` が使う。解釈に失敗したときは従来の部分一致に戻す(フェイルクローズ)。
  上流 Meltype には無い MeltKelp 側の仕組みなので、取り込みで衝突する心配は無い。テンプレート側にも同じ作りがある。
  #14 の `--repo` / `-R` は、全文の正規表現ではなくマージごとの語から取るようにした。
  agy レビューの指摘(CRLF のヒアドキュメントで終端を見失い素通しになる)を再現して直した。[[recurring-review-findings]] に実例を足した。

## [2026-10-18] ingest | PR #26 zenz で漢字変換の候補を文脈つきに並べ替える
- 新規: [[zenz-rerank]]
- 更新: [[context-rerank-lessons]], [[overview]], [[build-install]], [[engine-base-choice]], [[recurring-review-findings]]
- 備考: #2。`HybridConverter` を `ZenzRerankingConverter` で包み、imev の採点(`LlmScorer::Impl::Rank()`)を C# に移した。
  imev のプロンプトのタグが誤り(U+E200〜。正しくは U+EE00〜)で、imev #33 の「対数確率が鈍い」と、かなの候補を外した理由(写しのカタカナが勝つ)は
  これが原因だった。タグを直したうえで、かなの候補も採点に入れた(Issue の時点の「外す」から変えた)。llama.dll は b4846 のものしか読み込まない。
  英単語を挟んだ同じ入力の前の区間が文脈に入らない件は #24。
  実機の確認で 2 つ直した: モデルの読み込みを起動時に始める(最初の変換で始めると 1 回目が並べ替わらない)、文脈から前の行を外す
  (メモ帳の前の行「…講演」に引っぱられて 街の + こうえん → 公演 になった)。
  agy レビューの指摘(採点できなかった変換の後も、↑↓ の一覧が前の文脈の順で並ぶ)をテストで再現して直し、[[recurring-review-findings]] に種類を足した。
  2 回目の agy レビューの指摘 2 件(1 回の変換で予算を 2 回取って採点し直す・`--perf` の計測用ラッパーが文脈を渡さない)も直し、
  [[recurring-review-findings]] に種類を足した。3 回目の指摘(予算を過ぎた後の呼び出しで、控えてある採点を使わない)も再現して直した。
  ユーザーの判断で、この作業を MeltKelp 0.1.0 とした。上流の版 1.2.1 は変えず、MeltKelp の版を別に持つ([[overview]])。タグは `meltkelp-v0.1.0`。

## [2026-10-18] ingest | PR #30 zenz の採点を GPU(Vulkan)で動かせるようにする
- 更新: [[zenz-rerank]], [[build-install]]
- 備考: llama.cpp b4846 の Vulkan 版を CPU 版と並べて読み込める版に足し、`Install-ZenzRuntime.ps1 -Gpu` で置けるようにした。
  手元の RX 9060 XT で、1 キー cold の p95 が約 40ms → 17ms。初めての形の decode が 200ms 前後かかるのを実測で見つけ、
  読み込みの最後にトークンの数を変えて空回しするようにした。GPU では abort callback が効かないことも書いた。

## [2026-10-18] ingest | PR #32 zenz を CPU と GPU のどちらで動かすかを設定で選べるようにする
- 更新: [[zenz-rerank]], [[build-install]]
- 備考: CPU 版を `zenz\cpu`、Vulkan 版を `zenz\vulkan` に並べて置き、設定「zenz を GPU で動かす」で選ぶ。Vulkan 版 1 つで
  `n_gpu_layers` を切り替える案は、0 にしても GPU を使い続けるので、ユーザーの判断で採らなかった。同じ名前の DLL を 1 つのプロセスで
  読み替えられないので、切り替えは起動し直したときに効く。

## [2026-10-18] ingest | PR #34 zenz\vulkan が垂直タブに崩れていたのを直す
- 更新: [[zenz-rerank]], [[build-install]]
- 備考: PR #32 で文字列を置き換えたときに `\v` がエスケープとして解釈され、コメント・スクリプトのヘルプ・Wiki の 5 か所が崩れていた。動作には影響しない。

## [2026-10-18] ingest | #35 トレイのメニューにバージョンと zenz の状態を出す
- 更新: [[zenz-rerank]], [[upstream-update-stop]]
- 備考: 行番号の参照がずれたのを直した(upstream-update-stop)。

## [2026-10-18] ingest | PR #28 打ったキーの表示形式を選べるようにした(#27)
- 新規: [[typed-keys-display]]
- 更新: [[overview]]

## [2026-10-18] query | PR #38 入れ直しても起動中のアプリは古い Meltype IME の DLL を使い続ける
- 更新: [[build-install]]
- 備考: 打ったキーの表示がメモ帳では出てサクラエディタでは出ない、という報告を調べた結果。起動しっぱなしの 32 bit アプリが
  改名された古い DLL(`.old`)を読み込んだままだった。コードの変更は無い。

## [2026-10-18] ingest | PR #43 パッケージの削減で System.Security.Cryptography.dll が消え、zenz が読み込まれなかったのを直す
- 更新: [[build-install]]
- 備考: #40。`Build-Package.ps1` の削減は自己診断で読み込まれたアセンブリしか残さず、自己診断は zenz を読み込まない。落とし穴の節を足した。

## [2026-10-30] ingest | PR #45 agy レビューに上流の取り込み PR 向けの --upstream を足す
- 更新: [[recurring-review-findings]]
- 備考: 仕組みは `review/README.md`、手順は `upstream-sync` スキル。レビューで、PR の差分だけから対象を取ると
  上流の変更を捨てた衝突の解き方が見えない、と指摘されて直し、誤りの種類を台帳に足した。

## [2026-10-30] ingest | #46 上流 Meltype を v1.4.0 まで取り込む
- 更新: [[zenz-rerank]], [[overview]], [[typed-keys-display]], [[upstream-update-stop]], [[upstream-report-stop]](後の 3 つは行番号のずれだけ)
- 備考: 上流は `HybridConverter` に Zenzai を渡すようにしたので、zenz の並べ替えの包みをその上に載せ直した。消えた
  「IME 自動切替」の `_imm32` は上流どおり除いた。上流の CLAUDE.md は `@AGENTS.md`(上流の貢献ルール)の 1 行で、MeltKelp 側を残した。
  上流が足したワークフローのうち書き込む・予定で走る 6 本を止め、`ime-tests` は残した。v1.4.1-pre.1 以降(日英判定の作り替え・
  プレリリースの自動更新)は次の正式版で取り込む。Zenzai と並べ替えを両方 ON にしたときの扱いは #47。
