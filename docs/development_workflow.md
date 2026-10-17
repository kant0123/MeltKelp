# 開発ワークフロー(Git / CI)

このドキュメントは、AI エージェント(Claude Code 等)が複数セッションで並行作業することを
前提にした、MeltKelp の日常の開発運用を 1 枚にまとめたものである(上流 Meltype には無い、MeltKelp が足した文書。
上流の開発手順は [DEVELOPMENT.md](DEVELOPMENT.md))。

- **エージェント向けの実行ルール**は リポジトリ直下の [CLAUDE.md](../CLAUDE.md) にある。
  本書はその背景・全体像を人間が読むためのもので、内容が食い違った場合は
  `CLAUDE.md` を正とする。
- 知識の蓄積(`wiki/`)については [wiki_workflow.md](wiki_workflow.md) を参照。
  仕様は Issue に、実装後の現状は `wiki/` に置き、**Wiki 更新は機能 PR に同梱する**。
- MeltKelp は CD を採用していない(配布も本番環境も無い)。CI の内容は下記「CI」を参照。
- 上流 Meltype の取り込みは `upstream-sync` スキル(下記「上流の取り込み」)。

## 全体像

エージェントが行うのは Issue 起票から「CI 緑と agy レビューを確認して PR をマージする」まで。
マージ後は main への push で CI が再度走るだけで、デプロイは無い。手元の PC への反映は
ビルドして入れ直す(`wiki/operations/build-install.md`)。

```mermaid
flowchart TD
    issue["Issue 起票<br/>(仕様・受け入れ基準はここに書く)"]
    worktree["worktree で実装<br/>(着手前に wiki/index.md を読む = Query)"]
    wikiUpdate["wiki/ 更新を同じブランチにコミット (Ingest)"]
    push["git push -u origin &lt;branch&gt;"]
    pr["PR 作成"]
    prTests["tests (CI) が PR に対して走る"]
    build["build (上流の build.yml) が src/ などの変更時に走る"]
    gate{"CI 緑?"}
    review["agy レビュー<br/>python review/agy_review.py"]
    verdict{"APPROVE?"}
    triage{"指摘を 1 件ずつ評価"}
    fp["誤検知は gemini-review、スコープ外・未確認は MeltKelp に起票<br/>--triage で評価を記録"]
    report["起票・評価を記録し、人間に報告<br/>(マージしない)"]
    merge["gh pr merge &lt;PR番号&gt; --merge<br/>(エージェントの仕事はここまで)"]
    mainCommit["main に merge commit"]
    mainPush["main への push"]
    mainTests["tests (CI) が main に対して走る"]

    issue --> worktree --> wikiUpdate --> push --> pr
    pr --> prTests
    pr -.src/ などを変えたとき.-> build
    prTests --> gate
    build -.-> gate
    gate -->|No| worktree
    gate -->|Yes| review --> verdict
    verdict -->|Yes| merge
    verdict -->|差し戻し| triage
    triage -->|成立を確かめた指摘あり: 修正して push| worktree
    triage -->|すべて誤検知・スコープ外・未確認 WARNING| fp --> merge
    triage -->|未確認の CRITICAL あり| report
    merge --> mainCommit --> mainPush --> mainTests
```

## 作業の進め方

エージェント向けの**逐次手順はスキルに分けて置いてある**
(`.agent/skills/<name>/SKILL.md`。Claude Code なら `.claude/skills/` に置く)。
本節は人間が全体像を掴むための要約で、細かい罠は各スキルに書いてある。

| 局面 | スキル | 主な内容 |
| --- | --- | --- |
| 着手 | `worktree-start` | Wiki を読む → Issue 確保 → `origin/main` から worktree |
| 実装後 | `wiki-ingest` | 影響ページの更新 → `log.md` 追記 → wiki-lint |
| 完了 | `pr-finish` | push → PR → CI → agy レビュー → マージ → 後始末 → Issue クローズ → メインツリー更新 |
| CI 緑の後 | `agy-review` | agy でレビュー → 差し戻しを評価 → 修正ループ / 誤検知を起票してマージ |
| 作業中 | `file-issue` | スコープ外の問題をその場で起票(対象の判断・粒度・本文) |
| 依頼時 | `wiki-lint` | Wiki の健康診断(機械検査 → 矛盾・乖離の洗い出し) |
| 上流の更新時 | `upstream-sync` | worktree で `git merge upstream/main` → 衝突の解決 → PR → CI → agy レビュー → マージ |

### 1. 開始 — worktree を作る

```bash
git fetch origin
git worktree add ../MeltKelp-<名前> -b <branch> origin/main
```

- ブランチ名は `fix/xxx` / `feature/xxx` / `chore/xxx` / `docs/xxx`。
- **ローカル `main` ではなく `origin/main` から切る**。
- worktree はリポジトリの外の兄弟ディレクトリ(`C:\Users\kntat\Documents\Git\MeltKelp-<名前>`)に置く。
  ローカルドライブ(C:)で完結させる。imev では、ネットワークドライブ・UNC パスで Mozc のビルドが通らなかった。

複数セッションが同時に作業するため、1 つの作業ツリーを共有すると HEAD を奪い合い、
自分のコミットが他人のブランチ上に載る事故が起きる。worktree はその防止も兼ねる。

### 2. コミット

- コミットメッセージは `feat:` / `fix:` / `docs:` / `chore:` + 日本語の概要。
- **`git add` はファイルを明示指定する。** 他セッションの未コミット変更が
  作業ツリーに混ざっていることがあるため、`git diff --cached` で
  ステージ内容を確認してからコミットする。
- **`wiki/` の更新を同じブランチに含める。** コミットは分けてよい(`docs: wiki を更新`)が、
  PR は分けない。実装と知識の反映がずれると Wiki は腐る。

### 3. 完了 — PR を作って CI を通してマージ

```bash
git push -u origin <branch>
gh pr create --repo kant0123/MeltKelp --title "<一文>" --body "<対応 Issue / 変更内容 / Wiki / 実機での確認 / レビュー>"
gh pr checks <PR番号> --watch             # 成功を確認
python review/agy_review.py --pr <PR番号>  # agy レビュー
gh pr merge <PR番号> --merge
git push origin --delete <branch>
cd <メインツリー>                          # worktree の中からは削除できない
git worktree remove ../MeltKelp-<名前>
```

- **main へ直接 push・直接マージしない。** 必ず PR を経由する。
- **PR は `kant0123/MeltKelp` に立てる。** 上流 (yksr-melt/Meltype) には PR・Issue を出さない。
  GitHub のフォーク機能を使わず独立リポジトリにしているのは、フォークの中では `gh pr create` が既定で上流に向くため。
- CI が失敗したらマージせず原因を調査する。修正が困難なら PR は開いたまま報告する
  (勝手に close しない)。
- **`gh pr create --fill` を使わない。** 本文をコミットメッセージから生成するため、
  規約が要求する「対応 Issue」「Wiki」の節を満たせない。`--body` で明示する。
- **PR 本文・コミットメッセージに `Fixes #` / `Closes #` を書かない。** 自動クローズされると
  後段の `gh issue close --comment` が `already closed` で失敗し、実装内容の要約が
  Issue に残らないまま手順が中断する。素の `#<番号>` 参照に留める。
- **`gh pr merge` に `--delete-branch` を付けない。** worktree 内で実行すると `gh` が
  マージ後に `main` へ checkout しようとして
  `fatal: 'main' is already used by worktree at ...` で失敗する。
  **マージ自体は成功している**ので、エラーを見てマージ失敗と誤読しないこと
  (リモートブランチ削除に到達しないだけ。`git push origin --delete` で消す)。

## CI

### ブランチ保護 — これを入れないと CI はゲートにならない

**ワークフローを置いただけでは、マージは一度も止まらない。** GitHub Actions のジョブが赤くても
`gh pr merge` はそのまま通る。マージを止めているのは常に**ブランチ保護の必須チェック**であり、
ワークフローはその判定材料を作っているだけである。

**この設定はリポジトリ側にあり、テンプレートのコピーには引き継がれない。** 新しく作った
プロジェクトでは毎回手で有効化すること。飛ばすと、CI が緑でも赤でもマージできる状態のまま
「CI 緑を確認してマージ」という手順だけが回り続ける。

```bash
gh api --method PUT repos/kant0123/MeltKelp/branches/main/protection \
  --input .github/branch-protection.json
```

同梱の [.github/branch-protection.json](../.github/branch-protection.json) は
`checks` と `core-tests`(どちらも `test.yml` のジョブ)を必須チェックにする。上流の `build.yml` のジョブは
`src/` などを変えたときだけ走るので必須にしない(走らない PR が永久に pending になる)。採用していないワークフローの名前は `contexts` から
外すこと — **一度も実行されないチェックを必須にすると、PR が永久に pending のまま
マージできなくなる。**

| 設定 | 値 | 理由 |
| --- | --- | --- |
| `required_status_checks.contexts` | 採用したジョブ名 | `jobs:` 直下のキーであって、ワークフロー名 (`name:`) ではない |
| `required_status_checks.strict` | `false` | `true` にすると main が進むたび全 PR で再 push が要る |
| `required_pull_request_reviews` | `null` | **1 人開発では設定してはいけない。** 自分の PR は自分で承認できず、誰もマージできなくなる |
| `enforce_admins` | `false` | 障害時に管理者が手で復旧する逃げ道を残す |

**private リポジトリのブランチ保護は GitHub の有料プラン(Pro 以上)が要る。** 無料プランでは上の API が 403 を返し、
保護が掛からない。その場合マージを止めるのは hook と手順だけになる(`wiki/overview.md`「運用」)。

agy レビューはローカルで agy を動かすため CI のチェックにはならず、ブランチ保護では強制できない。
代わりに [.claude/hooks/check_agy_review.sh](../.claude/hooks/check_agy_review.sh) が
`gh pr merge` の直前に発火し、head SHA に対するレビュー記録が PR に無ければマージを差し止める
(詳細は下記「agy レビュー」)。hook は Claude Code を使っているときしか動かず、
GitHub の画面から押されたマージは素通しになる。

### CI — `tests` ワークフロー(MeltKelp が足したもの)

[.github/workflows/test.yml](../.github/workflows/test.yml)

| 項目 | 内容 |
| --- | --- |
| 契機 | `pull_request` と main への `push` |
| `checks` | 必須チェック名とジョブ名の一致・Python の構文・agy レビューの回帰テスト・hook のテスト・Wiki の整合・シェルの構文・PowerShell の構文と BOM |
| `core-tests` | `Meltype.Core.Tests`(Linux。品質テストの基準を含む) |

どの検査も「対象 0 件で成功」に落ちないようにしてある。上流のファイルで BOM が無い `.ps1` は、直すと上流の
取り込みで衝突するので、名前を挙げて除外している(`test.yml` の `$upstreamNoBom`)。

### CI — 上流の `build` ワークフロー

[.github/workflows/build.yml](../.github/workflows/build.yml)(上流のまま。変えない)

`src/`・`native/`・`dictionaries/`・`tools/`・`packaging/` などを変えた push と PR で、Windows でのビルド
(`dotnet build`・Meltype IME の C++)と `Meltype.Tests`、インストーラーの台本、Linux / macOS での `Meltype.Core.Tests` を回す。
main への push で `native/mozc` が変わっていれば Mozc ヘルパーもビルドする(30〜40 分)。タグの push で配布物を作るジョブもあるが、
MeltKelp はタグを push しないので走らない。

**private リポジトリでは Actions の分数に上限があり、Windows は 2 倍・macOS は 10 倍で数えられる。**
`build.yml` の `core` ジョブは macOS でも走るので、分数が足りなくなったら判断する(上流のファイルを変えるか、build を無効にして
MeltKelp 用のワークフローを別に足すか)。

### 上流の取り込み

上流の更新は `upstream-sync` スキルで、worktree の中で `git merge upstream/main` して PR で入れる。
rebase・squash はしない(上流の履歴を保ち、次の取り込みの merge base を正しく保つため)。
衝突の多くは、MeltKelp が上流のファイルに入れた差し込み口で起きる。差し込み口を数行に留めているのはそのため。

### 無効にした上流のワークフロー

GitHub 上で何かを書き込む・公開する上流のワークフローは、ファイルを消さずに `gh workflow disable` で止めてある
(消すと上流の取り込みのたびに衝突する)。止めたもの: `bot`・`cla`・`dotnet-update`・`linux`・`mac`・`notices`・
`pr-checks`・`sponsors`・`winget`。理由は `wiki/overview.md`「運用」。**上流が新しいワークフローを足したら、
取り込みの PR で中身を読み、止めるべきものは `gh workflow disable` する**(`upstream-sync`)。

### `wiki-check`(オプション。MeltKelp では未導入)

[.github/workflows-optional/wiki-check.yml](../.github/workflows-optional/wiki-check.yml)

PR に `wiki/` の更新が含まれているかを確認する。既定は**警告のみで success** するので
マージは止まらない。必須ゲートにしたい場合はワークフロー末尾の `exit 0` を `exit 1` に変える。
Wiki 更新が不要な PR には `wiki:skip` ラベルを付ける。

ローカル側の同等チェックとして
[.claude/hooks/check_wiki_updated.sh](../.claude/hooks/check_wiki_updated.sh) がある
(`gh pr create` の直前に発火し、Wiki 未更新なら PR 作成をブロックする)。

### `wiki-lint`(MeltKelp では `test.yml` の `checks` に含めている)

[.github/workflows-optional/wiki-lint.yml](../.github/workflows-optional/wiki-lint.yml)

`wiki/` の**中身が整合しているか**を機械チェックする(壊れた `[[リンク]]`・孤立ページ・
`index.md` の掲載漏れ・frontmatter の不備・`updated` と最終コミット日のズレ・
`related` の片方向参照)。違反があれば落ちる。`wiki-check` が「**Ingest したか**」を見るのに対し
こちらは「**中身が正しいか**」を見るので、役割が違い併用できる。

- **Wiki を運用するなら実質的に必須。** エージェントは 1 ページ内で完結する規約(frontmatter を
  書く、テンプレートに従う)はよく守るが、Wiki 全体にまたがる大域的な一貫性は自然には保てない。
  壊れるのは常に後者なので、機械に見張らせる。
- テンプレートは「`tests` に相乗りさせない」としている(CD が CI 全体の成否を待つため、Wiki の不整合で
  デプロイが止まる)。MeltKelp には CD が無いので `checks` に入れている。CD を足すなら分け直す。
- `fetch-depth: 0` が要る。`updated` を各ファイルの最終コミット日と突き合わせるため、既定の
  浅いクローンでは履歴が足りず、**スクリプトは日付の検査を黙って飛ばす**(通っているのに
  何も見ていない状態になる)。
- ローカルでは `node scripts/wiki-lint.js`。**コミットしてから**走らせる(未コミットだと
  `updated` が古いと言われる)。

### agy レビュー

CI のワークフローではなく、**CI が緑になった後にエージェントがローカルで実行する**
([review/agy_review.py](../review/agy_review.py))。差分を agy 経由で Gemini にレビューさせ、
結果を PR コメントに残す。手順は `agy-review` スキル、設計判断は [review/README.md](../review/README.md)。

- **CI 緑が前提。** スクリプトは `gh pr checks` が緑でなければ実行を拒否する。
- **記録は head SHA ごと。** push すると未レビューに戻る。
- 差し戻し(CRITICAL / WARNING)があれば、エージェントが指摘を評価する。妥当なら修正して
  push → CI → 再レビュー。すべて誤検知なら `kant0123/gemini-review` に起票し、
  `--triage` で評価を PR に記録してからマージする。
- `gh pr merge` の hook がレビュー記録(差し戻しなら評価の記録も)を確認する。
- 前提: `agy` がインストール・ログイン済みで、PATH に通っていること。
- **導入直後は強制が効かない。** hook もスクリプトもメインツリーから読まれるため、導入 PR が
  メインツリーに反映されるまではレビューを素通しする。
  導入手順と注意点は [review/README.md](../review/README.md) の「導入するときの注意」。
- ドメイン不変条件は `--domain` か環境変数 `REVIEW_DOMAIN` で切り替える
  (`ime` / `general` / `fintech` / `distributed` / `healthcare` / `embedded`)。未設定なら `ime`
  (MeltKelp 向けに足したもの。テンプレートの既定は `general`)。

### `label-hygiene`(オプション。MeltKelp では未導入)

[.github/workflows-optional/label-hygiene.yml](../.github/workflows-optional/label-hygiene.yml)

GitHub Issues / Projects 連携を使う場合の補助ワークフロー。Issue クローズ時に
`status:blocked` ラベルが残っていれば自動で外す。Issues 連携を使わないなら不要。

## やってはいけないこと

| 禁止 | 理由 |
| --- | --- |
| メインツリーで別ブランチへの `git checkout` | 複数セッションの HEAD 奪い合い |
| 上流 (yksr-melt/Meltype) への push・PR・Issue | MeltKelp は上流に出さない方針。`upstream` の push 先は `DISABLED` にしてある |
| 上流のファイルの整形・改名・移動・削除、上流のワークフローの削除 | 上流の取り込みのたびに衝突する |
| `upstream/main` の rebase・squash での取り込み | 上流の履歴が失われ、次の取り込みで同じ衝突が繰り返される |
| main へ直接 push・直接マージ | CI がゲートとして機能しない(マージ後に走るだけになる) |
| `git add -A` / `git add .` | 他セッションの未コミット変更を巻き込む |
| `gh pr create --fill` | PR 本文が規約(対応 Issue / Wiki)を満たさなくなる |
| PR 本文の `Fixes #` / `Closes #` | 自動クローズで `gh issue close --comment` が失敗し、要約が残らない |
| `gh pr merge --delete-branch`(worktree 内) | `main` への checkout に失敗する。マージ済みなのに失敗と誤読される |
| worktree 削除の失敗後に手で `rm -rf` / `Remove-Item` | git が working tree と認識しなくなり `--force` でも回復しない |

## トラブルシュート(worktree)

| 症状 | 確認すること |
| --- | --- |
| `git worktree remove` が必ず失敗する | シェルの作業ディレクトリが削除対象の中にある。`../MeltKelp-<名前>` は **worktree の中からでも自分自身に解決する**ので、パスを見ただけでは気づけない。メインツリーに `cd` してから実行する |
| `Permission denied` で削除できない | ディレクトリに ReadOnly 属性が付いている(クラウドストレージのミラー同期・バックアップツールが親を掴んでいる場合など)。`scripts/worktree-cleanup.ps1` で属性を外してから git に削除させる |
| `fatal: ... is not a working tree` で `--force` も効かない | 一度削除に失敗した後の状態。手で消さず `scripts/worktree-cleanup.ps1`(引数なし)で孤立エントリを掃除する |
| `git worktree list` に出ないディレクトリが残っている | 削除失敗の残骸。`-RemoveDirs` で消せるが**未コミットの変更ごと消える**ので中身を確認してから |
