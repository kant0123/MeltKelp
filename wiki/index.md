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

## Components — モジュール・機能単位 (`components/`)

- [[typed-keys-display]] — 設定「打ったキーを表示」の形式(ローマ字・表示しない・ひらがな・単語ごとに空白を入れたひらがな)。上流の ON / OFF のキーを残したままプルダウンにした仕組みと、単語の区切りに Microsoft IME の形態素解析を使う理由
- [[zenz-rerank]] — zenz で漢字変換の最初の候補を文脈つきに選び直す部品(HybridConverter のデコレーターと llama.cpp の採点器)。差し込み口・文脈の作り方・学習との順序・かなの扱い・ランタイムの置き場所と版の固定・GPU(Vulkan)での採点と CPU との切り替え

## Concepts — 横断的な仕組み・設計判断 (`concepts/`)

- [[context-rerank-lessons]] — imev の Mozc 文脈リランカーで踏んだ落とし穴(かな候補・元の順位の事前・学習との順序・採点の鈍さ・打ち切り)と、MeltKelp の zenz の並べ替えでそれぞれどうしたか。採点の鈍さとカタカナの写しの原因は imev のプロンプトのタグの誤りだった
- [[engine-base-choice]] — zenz の並べ替えを載せる土台に Meltype を選んだ理由。imev での Mozc 本家・Ohagey(azooKey + Zenzai)の評価と、Meltype に移した決め手
- [[learned-short-words]] — 英字に直して覚えた短い語(no・go)を、英字の略語の後ろの助詞(APIno)にまで使わない理由と、再現するときの罠(学習は検出器に渡さないと効かない)
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
