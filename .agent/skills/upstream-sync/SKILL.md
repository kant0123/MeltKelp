---
name: upstream-sync
description: >
  上流 Meltype (yksr-melt/Meltype) の更新を MeltKelp に取り込む手順。worktree で git merge upstream/main し、
  衝突を方針どおりに解き、PR → CI → agy レビュー → マージまで進める。上流が足したワークフローの無効化、
  タグを push しない理由、差分が agy レビューの上限を超えたときの分け方を含む。
  「上流を取り込んで」「Meltype の新しい版を入れて」「upstream に追いついて」と言われたとき、
  上流の新しいリリース・修正を MeltKelp で使いたいときに使う。上流へ何かを出す作業には使わない(出さない)。
---

# 上流 Meltype の取り込み

MeltKelp は上流の全履歴を持つ独立リポジトリで、上流は remote `upstream`(push 先は `DISABLED`)。
取り込みは **merge** で行い、rebase・squash・cherry-pick はしない。上流の履歴をそのまま持っていれば、
次の取り込みの merge base が正しく決まり、同じ衝突を何度も解かずに済む。

**上流には何も出さない。** 取り込みで上流の不具合に気付いても、起票先は `kant0123/MeltKelp`。

## 1. 何が来るかを見る

```bash
git fetch upstream --no-tags
git fetch origin
git log --oneline --no-merges origin/main..upstream/main | head -50
git diff --stat origin/main...upstream/main | tail -5
git diff --name-status origin/main...upstream/main -- .github/workflows
```

- **タグは取らない(`--no-tags`)し、push もしない。** 上流のタグ `v*` が origin に乗ると、上流の `build.yml` の
  `package` ジョブがリリースを作ろうとする。版は `upstream/main` のコミットとリリースノートで確かめる
  (`git ls-remote --tags upstream` で一覧だけ見るのはよい)。
- 次のファイルが変わっていたら、MeltKelp の前提に関わるので差分を読む。
  - `.github/workflows/` — 足されたワークフローは手順 5 で無効にするか決める
  - `native/tip/`・`src/Meltype/Tip/` — Meltype IME とのパイプの形式。上流の変更はそのまま受け入れる(上流の署名した DLL と
    合わせるのが目的なので、MeltKelp 側で戻さない)
  - `src/Meltype/Updater.cs`・`packaging/update.ps1`・`packaging/install.ps1` — 自動更新を止める差し込み(#3。`wiki/concepts/upstream-update-stop.md`)が効き続けるか。`Updater.cs` が衝突したら同じページの「上流の取り込みで衝突したら」
  - `src/Meltype/Composition/CompositionService.cs`・`src/Meltype.Core/Composition/HybridConverter.cs`・
    `src/Meltype.Core/Composition/KanjiConversion.cs` — zenz の差し込み口と、MeltKelp が実装するインターフェース(#2)
  - `src/Meltype.Core/Config/Settings.cs` — 設定キーの意味・既定値
- **差分が大きいとき(agy レビューの上限 30 万文字を超えそうなとき)は分けて取り込む。** 上流のタグ・リリースの
  コミットごとに `git merge <コミット>` で段階的に入れ、PR も分ける。`--max-chars` で上限を上げて 1 回で通さない
  (レビューの精度が落ちる)。上限に当たらないよう、上流の更新はこまめに取り込む。

## 2. Issue を確保して worktree を切る

```bash
gh issue create --repo kant0123/MeltKelp --label enhancement \
  --title "上流 Meltype を <版 or upstream/main の短い SHA> まで取り込む" \
  --body "<取り込む範囲 (origin/main の merge base..upstream/main) / 手順 1 で見つけた前提に関わる変更>"
git worktree add ../MeltKelp-upstream-sync -b chore/upstream-sync-<版> origin/main
cd ../MeltKelp-upstream-sync
git merge --no-ff upstream/main -m "chore: 上流 Meltype <版> を取り込む"
```

worktree はローカルドライブ(C:)に置く(`worktree-start`)。

## 3. 衝突を解く

方針は「**上流のファイルは上流を正とし、MeltKelp の差し込み口だけを載せ直す**」。

| 衝突したもの | 解き方 |
| --- | --- |
| 上流のファイルの、MeltKelp が差し込み口を入れた箇所 | 上流側(theirs)を取り、MeltKelp の差し込み口の数行だけを載せ直す。周りの書き方を上流に合わせる |
| 上流のファイルで、MeltKelp が意図せず変えていた箇所 | 上流側を取る(本来は差分が無いはずの場所。原因を調べて Issue にする) |
| MeltKelp だけのファイル(`CLAUDE.md`・`.agent/`・`.claude/`・`review/`・`wiki/`・`scripts/`・`test.yml` など) | MeltKelp 側(ours)。上流が同名のファイルを足したときは、どちらも残せる名前にできないか考え、無理なら止めて人間に聞く |
| `.gitignore` | 両方の行を残す。MeltKelp の行は末尾の「MeltKelp:」の節にまとめておく |
| `src/Meltype/Meltype.csproj` の `<Version>` など版の表記 | 上流側。版は上流に合わせる(自動更新の判定にも使われるので、変えるなら #3 の仕組みで扱う) |
| `.github/workflows/` の上流のファイル | 上流側。MeltKelp は中身を変えず、`gh workflow disable` で止めている |

- **上流が差し込み口の周りを作り替えて、載せ直す場所が無くなったときは無理に合わせない。** マージを止め
  (`git merge --abort`)、何が変わったかと MeltKelp 側をどう直すかを Issue に書いて人間に判断を渡す。
- **上流が MeltKelp の使っているインターフェースを変えたときは、MeltKelp の新しいファイルの側を直す。**
  上流のファイルを MeltKelp に合わせて戻さない。
- **衝突の解決で上流のファイルを整形・並べ替えしない。** 次の取り込みでまた衝突する。
- 解いたら、上流のファイルへの MeltKelp の差分が差し込み口だけに収まっているかを見る:

```bash
git diff upstream/main -- src native packaging tools dictionaries | head -100
```

## 4. ビルドとテスト

```bash
dotnet build Meltype.sln -c Release
dotnet run --project src/Meltype.Tests -c Release
python -m pytest review/tests -q
node --test .claude/hooks/test/*.test.mjs scripts/test/*.test.mjs
```

そのうえで、ビルドして入れ直し、**メモ帳と Chrome で、Meltype IME と Meltype キーボードの両方で**変換を確かめる
(`wiki/operations/build-install.md`)。上流の変更は CI だけでは実際のアプリでの入力まで確かめられない。
入れ直したら、上流の更新が止まったままかも見る(トレイの「更新」に「自動で更新する」が無く、「今すぐ更新を確認」で止めている旨が出る)。

## 5. 上流が足したワークフローを止める

上流が `.github/workflows/` にファイルを足していたら、中身を読んで、GitHub 上で何かを書き込む・公開する・
予定で走るもの(bot・リリース・Issue の自動作成・スポンサー表示の更新・winget への提出など)は無効にする。

- `pull_request` で走るものは、この PR の時点で走る(読み取りだけのことが多いが、中身を確かめる)。
- `push` で走るものは、**マージした瞬間に main で走る。** 止めるべきものがあるときは、マージの直前に Actions を
  リポジトリごと止め、マージ → 無効化 → 再開の順にする(ワークフローは default branch に載るまで `gh workflow disable` できない):

```bash
gh api -X PUT repos/kant0123/MeltKelp/actions/permissions -F enabled=false
gh pr merge <PR番号> --merge
gh workflow list --all
gh workflow disable <足されたワークフロー名>
gh api -X PUT repos/kant0123/MeltKelp/actions/permissions -F enabled=true
```

- ファイルは消さない(次の取り込みで衝突する)。止めたものは `docs/development_workflow.md` の
  「無効にした上流のワークフロー」と `wiki/overview.md`「運用」に足す。
- Windows のビルドとテスト(`build.yml`)のように、MeltKelp でも役に立つ検査は残す。

## 6. PR → CI → agy レビュー → マージ

`pr-finish` に従う。PR 本文には次を書く。

- 取り込んだ範囲(`git merge-base` の SHA..`upstream/main` の SHA)と、上流の版
- 衝突したファイルと解き方(手順 3 のどの行に当たるか)
- 手順 1 で読んだ、前提に関わる変更と、MeltKelp 側でしたこと
- 足されたワークフローと、止めたかどうか
- 実機での確認の結果

agy レビューの差分には上流の変更がすべて入る。上流のコード自体への指摘は、MeltKelp の差し込み口や前提
(パイプの形式・自動更新の停止・入力内容の扱い)を壊していない限り「スコープ外」とし、直すべきなら
`kant0123/MeltKelp` に起票する(上流には報告しない)。

## 7. 後始末

`pr-finish` の手順 6〜8。取り込んだ版と、次に気を付けること(作り替えられた差し込み口など)を
`wiki/log.md` に残す。
