# Git ワークフロー & 完了の定義

> `CLAUDE.md` の「フォークの不変条件」「Git ワークフロー」「完了の定義」「agy レビュー」節の写し。
> 規約を変えるときは `CLAUDE.md` と同じ PR でここも直す(片方だけ直すと食い違う)。
> 手順は `.agent/skills/` の各スキル(`worktree-start` / `wiki-ingest` / `pr-finish` / `agy-review` / `file-issue` / `upstream-sync`)。

## フォークの不変条件(上流 Meltype との関係)

背景は `wiki/overview.md`、取り込みの手順は `upstream-sync` スキル。

1. **上流には何も出さない**:
   - yksr-melt/Meltype へ push・PR・Issue・コメント・取り込みの依頼をしない。
   - Issue と PR はすべて `kant0123/MeltKelp` に立てる。`gh issue create` / `gh pr create` には `--repo kant0123/MeltKelp` を付ける。
2. **上流のファイルとの差分を最小にする**:
   - 機能は新しいファイル(新しいクラス・デコレーター・別の設定キー)で足し、上流のファイルへの変更は差し込み口の数行に留める。
   - 上流のファイルを整形・改名・移動・削除しない(取り込みのたびに衝突する)。上流のワークフローも消さずに `gh workflow disable` で止める。
3. **公式と同じ識別子を変えない**:
   - インストール先(`%LOCALAPPDATA%\Programs\Meltype`)・データフォルダー(`%LOCALAPPDATA%\Meltype`)・TSF の CLSID とプロファイル GUID(`native/tip/Globals.h`)・パイプ名(`TipServer.PipeName`)は上流と同じにしておく。
4. **`native/tip/` とパイプのやり取りの形式を変えない**:
   - 上流が将来署名した Meltype IME の DLL をそのまま差し替えて使えるようにするため。MeltKelp の機能は `Meltype.exe` 側(C#)で完結させる。
5. **上流のリリースによる自動更新を止めたままにする**:
   - `src/Meltype/UpstreamUpdate.cs` の判定と `Updater.cs` などへの 1 行の差し込みで止めている(#3)。上流の取り込みで差し込みを落とさない(`wiki/concepts/upstream-update-stop.md`)。
6. **上流の取り込みは worktree で `git merge upstream/main` し、PR で入れる**(rebase・squash しない。上流の履歴を保つ)。
7. **ライセンスは GPL-3.0 のまま**:
   - 上流の著作権表示・`LICENSE`・`THIRD-PARTY-NOTICES.md` を消さない。MeltKelp が足すソースにも `SPDX-License-Identifier: GPL-3.0-or-later` を付ける。

## 常に守る基本規約

複数のエージェントセッションが同じリポジトリで同時に作業する前提。作業ツリーを共有すると
HEAD の奪い合いで自分のコミットが他人のブランチに乗るため、実装は必ず専用 worktree で行う。

1. **main へ直接 push・直接マージしない**:
   - 必ず PR を経由し、CI の成功(`gh pr checks`)を確認してから `--merge` でマージする。
2. **実装作業はメインツリーで行わない**:
   - `git worktree add ../MeltKelp-<作業名> -b <branch> origin/main` で作った**リポジトリ外の**兄弟ディレクトリで作業する。
   - ブランチ名は `fix/` / `feature/` / `chore/` / `docs/` を前置し、**ローカル `main` ではなく `origin/main` から切る**。
   - リポジトリも worktree もローカルドライブ(C:)に置く(ネットワークドライブ・UNC パスでは Mozc のビルドが通らず、管理者の PowerShell から見えない。`wiki/operations/build-install.md`)。
3. **`git add` はファイルを明示指定する**:
   - `-A` / `.` は使わない。他セッションの未コミット変更が混ざりうるため、`git diff --cached` で確認してからコミットする。
4. **worktree の安全な後始末**:
   - 使い終わったら、メインツリーに `cd` してから `git worktree remove` で消す。
   - 失敗しても手で `rm -rf` / `Remove-Item` しない(git が working tree と認識しなくなり回復しない)。復旧は `scripts/worktree-cleanup.ps1`。
5. **事前承認済みの操作**:
   - push・PR 作成・マージ・ブランチ削除・worktree 削除は事前承認済み。`git status` で意図しない変更が混ざっていないか確認してから実行する。
   - CI 成功と agy レビューの完了まで確認したら、マージしてよいかを改めて聞かない。
6. **失敗時はマージしない**:
   - CI が失敗したらマージせず原因を調べる。修正が困難なら PR は開いたまま報告する(勝手に close しない)。
   - タスクが失敗・中断した場合も勝手にマージ / 削除せず、worktree は残して報告する。
7. **PR / Issue での罠回避**:
   - `gh pr create --fill` は使わない(必須節が満たせないため `--body` で明示指定)。
   - `Fixes #` / `Closes #` を使わない(マージ時に自動クローズされると、後続の `gh issue close --comment` が失敗して要約が残らない)。素の `#<番号>` 参照に留める。
   - `gh pr merge --delete-branch` を使わない(worktree 内ではローカル main への checkout 失敗がマージ失敗と誤読される)。ブランチ削除は `git push origin --delete` で行う。

コミットメッセージは `feat:` / `fix:` / `docs:` / `chore:` プレフィックス + 日本語の概要。
上流から取り込んだコミットのメッセージ(英語・上流の Issue 番号)はそのまま残す。

## agy レビュー

PR の CI が緑になった後、マージの前に、差分を agy (Antigravity CLI) 経由で Gemini に 1 回レビューさせる。
手順は `agy-review` スキル、仕組みは `review/README.md`。既定のドメインは IME 向けの `ime`(`review/domain_invariants.json`)。

- **レビューは必須。順序は push → CI → `python review/agy_review.py` → (差し戻しなら評価)→ マージ。**
  レビュー記録の無いマージは `gh pr merge` の hook が差し止める(回避手段は置いていない)。
- **`gh pr merge` には必ず PR 番号を付ける**(hook は番号で PR を特定する)。
- **レビューは head SHA ごとに有効。** 修正を push したら CI から回し直す。
- **差し戻しは 1 件ずつ評価し、妥当な指摘が 1 件でもあれば修正する。** 誤検知と判定するなら成立しない理由を
  コードの場所つきで書き、`kant0123/gemini-review` に `false-positive` で起票してから `--triage` で記録する。
  見逃し(通過した差分が原因の不具合)も `false-negative` で起票する。
- **妥当と判定するのは、成立を確かめた(再現・失敗するテスト・起きる経路のコード)指摘だけ。**
  確かめる手段がこの環境に無い指摘は「未確認」として修正せず、このリポジトリに起票して `--triage` に書く。
  CRITICAL が未確認ならマージしない(merge hook も差し止める)。
- **妥当だった指摘は、誤りの種類として `wiki/concepts/recurring-review-findings.md` に修正と同じ PR で残す。**
  着手時と push 前にこのページを読む。
- **レビューが失敗(agy のエラー・空出力)したままマージしない。**
- **レポート(`review/work/`)をコミットしない。** 残す価値のある知見は `wiki/` に還元する。

## 完了の定義 (Definition of Done)

- 変更に関係するテストをローカルで実行し、パスを確認している。
- CI(`gh pr checks`)が全件パスしている。
- `src/`・`native/`・`packaging/`・`dictionaries/` を変えた場合、ビルドして入れ直し、**メモ帳と Chrome で、Meltype IME と
  Meltype キーボードの両方で**変換を確かめている(CI は TSF への登録と実際のアプリでの入力を見ないため)。PR 本文に、確かめた例文と結果を
  書いている(例文は自分で用意したもの)。Chrome で Meltype IME が読み込まれないのが ESET による既知の制約のときは、その旨を書く。
- 上流を取り込んだ場合も、上の実機確認までしている(`upstream-sync`)。
- `wiki/` の該当ページと `wiki/log.md` を同じ PR で更新している(更新不要と判断した場合は PR 本文にその理由を一行書いている)。
- Wiki を更新した場合、**コミット後に** `node scripts/wiki-lint.js` が通っている。
- 作業中に見つけてこの PR で直さないと決めた問題を、すべて Issue 化している(1 件も無ければ、それ自体を確認済みとする)。
- 変更内容の要約と手動確認結果を報告している。起票した Issue があれば番号を挙げている。
- PR の最新 head が agy レビューを通っている(APPROVE、または差し戻しを評価して誤検知を起票済み)。結果を PR 本文の「レビュー」節に書いている。
