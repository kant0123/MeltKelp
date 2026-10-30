---
type: concept
summary: MeltKelp とは何か・なぜ独立リポジトリのフォークで、上流に出さず、公式を置き換える前提なのか。各ページへの入口
updated: 2026-10-30
related: [engine-base-choice, context-rerank-lessons, build-install, recurring-review-findings, upstream-update-stop, upstream-report-stop, zenz-rerank, typed-keys-display]
---

# MeltKelp 全体像

> このページは Wiki の入口。**新しくこのプロジェクトに関わる人間 / エージェントが
> 最初に読む 1 枚**として維持する。詳細は各ページへリンクで逃がし、ここは俯瞰に徹する。

## これは何か

MeltKelp(とろろ昆布)は、Windows 向けの日本語入力 Meltype(yksr-melt/Meltype、GPL-3.0)の個人用フォーク。
Meltype の強みである英語と日本語の自動判定をそのまま使い、漢字変換の候補を小型の言語モデル zenz で
文脈つきに並べ替える([[zenz-rerank]]、#2)。ほかに上流の Meltype に足したのは、上流の自動更新を止める変更 [[upstream-update-stop]] と、報告の導線を上流に向けない変更 [[upstream-report-stop]]。

前身は imev(kant0123/imev。Mozc 本家に zenz の文脈リランカーを載せた個人用 IME)。imev の土台を Meltype に移すと
決めて立ち上げた。理由は [[engine-base-choice]]、imev で得た並べ替えの知見は [[context-rerank-lessons]]。

## 構成

上流の構成のまま(上流の [DEVELOPMENT.md](../docs/DEVELOPMENT.md) と [TSF_DESIGN.md](../docs/TSF_DESIGN.md))。

- `Meltype.exe`(`src/Meltype`・`src/Meltype.Core`)— タスクトレイに常駐し、キーボードフックで打鍵を受け、英日の判定・変換・
  候補の一覧を持つ本体。MeltKelp が手を入れるのはここだけ
- Meltype IME(`native/tip`)— TSF の DLL。入力欄に直接書き、中身は名前付きパイプで `Meltype.exe` に問い合わせる。
  候補の一覧の中身は `Meltype.exe` が決めるので、並べ替えは DLL を変えずにできる
- Mozc の変換ヘルパー(`native/mozc`)と Microsoft IME — 漢字変換のエンジン。`HybridConverter` が両方を組み合わせる。
  上流がエンジンに渡す文脈は短い(キャレットの前の同じ文の日本語・最大 10 文字)

動作モードは 2 つあり、確認はいつも両方で行う: **Meltype IME**(TSF で入力欄に直接入力)と
**Meltype キーボード**(Meltype 自身の変換ボックスで入力し、確定した文字列を送る)。

## 主要な設計判断

- **GitHub のフォーク機能を使わず、独立したリポジトリにしている。** フォークの中では `gh pr create` が既定で上流に
  向き、上流に PR を出す事故が起きうるため。上流は remote `upstream` として持ち、push 先を `DISABLED` にしてある。
  上流の全履歴をそのまま持っているので、取り込みは `git merge upstream/main` で済む(`upstream-sync` スキル)。
- **上流には何も出さない(push・PR・Issue・取り込みの依頼をしない)。** 上流の作者は LM の常時実行を見送っている
  (上流 #270 は not_planned)。MeltKelp の変更は上流の方針と合わないので、取り込みを求めず独自に持つ。
  Issue・PR はすべて `kant0123/MeltKelp` に立てる。
- **公式の Meltype と共存させず、置き換える前提。** インストール先(`%LOCALAPPDATA%\Programs\Meltype`)・
  データフォルダー(`%LOCALAPPDATA%\Meltype`)・TSF の CLSID・パイプ名を上流と同じにしておく。
  共存させるには識別子を全部変える必要があり、そうすると (1) 上流の取り込みのたびに衝突し、(2) 上流が将来
  署名した Meltype IME の DLL をそのまま使えなくなる。Chrome で自前ビルドの IME が動かないのは、署名の無い
  TIP DLL を ESET が止めるためで、土台を替えても直らない([[build-install]])。DLL とパイプの形式を変えずに
  おけば、上流が署名した DLL に差し替えるだけで済む可能性が残る。
- **上流との差分を最小にする。** 機能は新しいファイルで足し、上流のファイルは差し込み口の数行だけ変える。
  上流のワークフローもファイルを消さずに無効にしている(下の「運用」)。
- **上流のリリースによる自動更新を止めている。** 置き換え前提なので、上流の `Updater.cs` のままだと公式版で
  MeltKelp が上書きされる(上流の版 1.2.1 をそのまま持つので「公開版」と判定される)。確認・ダウンロード・適用・
  トレイからの手動の更新のすべてを、設定の「自動で更新する」に関わらず止めた(#3)。仕組みと、上流の取り込みで
  `Updater.cs` が衝突したときの直し方は [[upstream-update-stop]]。
- **MeltKelp の版は上流の版と別に持つ。** `src/Meltype.Core/Config/MeltKelpInfo.cs:12 MeltKelpInfo` の `Version`(#2 の zenz の並べ替えを
  0.1.0 とした。2026-10-18、ユーザーの判断)。「Meltype について」・`--version`・報告の実行環境に「MeltKelp 0.1.0 (Meltype 1.2.1 ベース)」の形で出す。
  上流の版(`Meltype.csproj` の 1.2.1)は変えない。`IsPublicRelease`(メジャー 1 以上)で公開版かを決めていて、0.x にすると
  プレビュー版の扱いに変わり、上流の取り込みのたびに衝突するため。リリースのタグは `meltkelp-v<版>`(`v*` を origin に push すると、
  上流の `build.yml` の `package` ジョブがリリースを作ろうとする)。
- **報告の導線を上流に向けない。** トレイの「不具合の報告・提案」などは上流のままだと上流の Issue の画面を開き、
  誤判定の報告では入力内容が URL に入る。報告の URL を MeltKelp の Issue の画面(クエリ無し)に向け、
  トレイの「不具合の報告・提案」「直した誤判定を報告」は出さない(#8、[[upstream-report-stop]])。
- **設定「打ったキーを表示」の形式を選べる。** ローマ字・表示しない・ひらがな・単語ごとに空白を入れたひらがな(#27、[[typed-keys-display]])。
  上流の ON / OFF のキーはそのまま持ち、単語の区切りは Microsoft IME の形態素解析を使う。

## 触る前に知っておくこと

- コード・上流のコミット・上流の `docs/` にある `#123` は**上流の** Issue 番号。MeltKelp の Issue とは別物
- 上流の `docs/`(DEVELOPMENT.md など)は上流の文書なので書き換えない。MeltKelp の知識はこの `wiki/` に置く
- `README.md` は MeltKelp 用に書き直してある(#18)。上流の取り込みで衝突したら MeltKelp 側を残す。上流の README の
  ダウンロード・Discord・Sponsors・協力者の欄は MeltKelp には当てはまらない(協力者は上流の README に任せ、リンクだけ張った)
- 入力内容は個人情報。上流のログ(`meltype.log`)は設定が OFF でも判定した語の先頭の数文字を含むので、Issue・PR に貼らない
- レビューで繰り返し差し戻された誤りの種類は [[recurring-review-findings]]。着手時と push 前に読む

## 運用

配布・デプロイは無い。手元の PC にビルドして入れるだけ([[build-install]])。テンプレート由来の CD 一式(`deploy/`)は持ち込んでいない。

- **リポジトリは private**(2026-10-17 作成時の判断)。private では Actions の分数に上限があり、
  Windows のランナーは 2 倍、macOS は 10 倍で数えられる。
- **CI は 2 本立て。** MeltKelp が足した `test.yml`(Linux。レビュー基盤・hook・Wiki・構文・`.ps1` の BOM・
  `Meltype.Core.Tests`)と、上流のまま残した `build.yml`(Windows でのビルドと `Meltype.Tests`、`src/` などを変えたときだけ)。
  どちらも実際のアプリでの入力は見ないので、入力の挙動を変えたら実機での確認が要る([[build-install]])。
- **無効にした上流のワークフロー**(ファイルは残し、`gh workflow disable` で止めた。2026-10-17):
  - `bot`・`cla`・`notices` — 上流の Issue・PR に bot がコメント・ラベル付け・CLA の確認をするもの
  - `pr-checks` — 上流の PR 運用(本文の「確かめること」の再現・精度の比較)に合わせたもの。精度の比較は役に立つが、
    上流の PR の書式に依存するので止めた。必要になったら MeltKelp 用に別のワークフローで足す
  - `sponsors`(README のスポンサー表示を更新する PR を作る)・`dotnet-update`(.NET の更新を Issue にする)— 予定で走って書き込む
  - `winget`(winget に提出する)— リリースで走る。MeltKelp はリリースしない
  - `mac`・`linux` — Mac 版・Linux 版のビルド。MeltKelp は Windows だけで、macOS は分数を 10 倍で食う
  - 上流 v1.4.0 で足されたもの(#46。2026-10-30): `conflicts`・`policy`(bot が上流の Issue・PR にラベル・コメントを付ける。
    `pull_request_target`・予定で走る)、`code-scanning`(CodeQL の結果を Issue・PR に書く)、`codeql`・`quality`(結果を
    code scanning に上げる。予定・main への push で走る)、`web`(上流の Web サイトの検査。MeltKelp は使わない)
- **残した上流のワークフロー:** `build` と `ime-tests`(v1.4.0 で足された、Core の入力の回帰テスト。読み取りだけ)。`core` ジョブが macOS でも走るので、分数が足りなくなったら判断が要る
  (`docs/development_workflow.md`)。
- **ブランチ保護**: 正は `.github/branch-protection.json`(必須チェックは `checks` と `core-tests`)。保護設定は
  GitHub 側にあり、リポジトリのファイルからは見えない。**`test.yml` のジョブ名を変えたら、このファイルと保護設定の
  両方を直す**(`test.yml` の `checks` が一致を検査する)。
  **保護は掛けない運用にしている(2026-10-17、ユーザーの判断。#5)。** 適用を試すと「Upgrade to GitHub Pro or make this
  repository public」で 403 になった(private で無料プランのため)。GitHub Pro にする・public にする、のどちらも選ばず、
  private の無料プランのまま保護なしで運用する。**保護の適用を試し直したり、同じ判断を聞き直したりしない。**
  CI が赤でも GitHub 上はマージできるので、マージを止めているのは `pr-finish` の手順と、マージの直前に発火する hook
  (`.claude/hooks/check_agy_review.sh`)だけ。hook は head SHA ごとの agy レビューの記録を見て、`agy_review.py` は CI が緑の
  head にしかレビューを付けないので、Claude Code からのマージに限れば「CI 緑 + レビュー済み」が守られる。GitHub の画面からの
  マージは素通しになる。`gh pr merge` に `--repo` / `-R` が付いていれば、hook は `agy_review.py --check --repo` で
  そのリポジトリの PR として判定する(#14。付いていないと別リポジトリの PR 番号を MeltKelp の PR として引いて止まる)。
  別リポジトリでも判定は外さない。`branch-protection.json` は、プランか公開範囲を変えたときのために正として残している。
  hook が見るのは「実際に実行されるコマンド」だけで、Issue や PR の本文・`grep` のパターンなど引数や引用符の中の文字列には
  反応しない(#6)。`--repo` / `-R` も、そのマージのコマンドの語から取る。読み取りは `.claude/hooks/command_match.mjs` が
  受け持ち(`check_wiki_updated.sh` の PR 作成の判定も同じ)、`&&` `;` `|` 改行・`$(...)`・`bash -c` / `eval` の引数まで辿る。
  node が無い・解釈に失敗したときは、従来の部分一致に戻して止める側に倒す。`python -c` など別言語から組み立てる呼び出しは読まない。
- agy レビューは IME 向けのドメイン `ime`(`review/domain_invariants.json`)で回す。テンプレート既定の `general` は
  Web 向けで、変換を止めない・入力内容を出さない・UI スレッドの時間上限・上流との差分の最小化の観点を持たない。
