# Index

Wiki 全ページのカタログ。質問に答えるとき・作業に着手するときは、まずこのファイルを読んで
関連ページに当たりを付ける。

**下のカタログは各ページの frontmatter の `summary` から生成される。**
要約を直すときはページ側の `summary` を直し、`node scripts/wiki-lint.js --write-index`
(`package.json` があれば `npm run wiki:lint -- --write-index`)を走らせる。
ここを手で書き換えても次の再生成で消える。「未作成ページ」節は生成領域の外なので手で編集する。

<!-- wiki-index:start -->
<!-- ここから下は scripts/wiki-lint.js が各ページの frontmatter の summary から生成する。
     手で書き換えても次の再生成で消える。要約を直すときはページ側の summary を直し、
     `node scripts/wiki-lint.js --write-index` を走らせる。 -->

## 全体像

- [[overview]] — MeltKelp とは何か・なぜ独立リポジトリのフォークで、上流に出さず、公式を置き換える前提なのか。各ページへの入口

## Concepts — 横断的な仕組み・設計判断 (`concepts/`)

- [[context-rerank-lessons]] — imev の Mozc 文脈リランカーで踏んだ落とし穴(かな候補の除外・元の順位の事前・学習との順序・採点の鈍さ・250ms の打ち切り)と、MeltKelp で zenz を載せるときの当てはめ方
- [[engine-base-choice]] — zenz の並べ替えを載せる土台に Meltype を選んだ理由。imev での Mozc 本家・Ohagey(azooKey + Zenzai)の評価と、Meltype に移した決め手
- [[recurring-review-findings]] — レビューで妥当と判定された指摘を誤りの種類ごとに集約した台帳。着手時と push 前に読む
- [[upstream-report-stop]] — トレイの「不具合の報告・提案」「直した誤判定を報告」をトレイから消し、報告の URL が上流 Meltype の Issue 画面を開かないようにした理由と仕組み。上流の取り込みで AppInfo.cs・TrayApplicationContext.cs が衝突したときに読む
- [[upstream-update-stop]] — 上流 Meltype のリリースによる自動更新(確認・ダウンロード・適用・手動)を MeltKelp で止めた理由と仕組み。上流の取り込みで Updater.cs が衝突したときに読む

## Operations — 運用手順・障害対応 (`operations/`)

- [[build-install]] — MeltKelp をビルドして手元の PC に入れ直し、実機で変換を確かめる手順。ローカルドライブで完結させる理由と、Chrome で Meltype IME が動かない件(ESET と署名)
<!-- wiki-index:end -->

## 未作成ページ (リンクだけ存在する / これから書く)

2 つの役割を兼ねる節。

1. 他ページから `[[...]]` で参照されているが実体が無いページの控え。次に書くべきページの候補リスト
2. **これから作るページ名の予約。** 複数セッションが同時に走る場合、実体を書く前にここへ名前を
   控えておかないと、同じ概念のページが別名で二重に作られる。着手時にこの節を読み、
   予約済みの名前と衝突していないか確認する

実体を作ったらこの節から消す(消し忘れは lint が落とす)。

<!-- 例: - [[rate-limiting]] — [[external-api]] から参照されている -->

_(なし)_
