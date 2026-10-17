#!/usr/bin/env bash
# [オプション] agy レビュー — マージ差し止め hook (Claude Code / PreToolUse / matcher: Bash)
#
# `gh pr merge` の直前に発火し、PR の head SHA に対する agy レビューの記録があるかを見る。
#   - レビューが無い                         → 差し止め
#   - 差し戻し (CHANGES_REQUESTED) で評価が無い → 差し止め
#   - APPROVE、または差し戻しを評価済み        → 通す
# 判定の本体は review/agy_review.py --check。ここはコマンドの振り分けだけ。
#
# レビューは必須なので回避手段は置かない。hook は Claude Code 経由のマージにしか効かず、
# GitHub の画面から押されたマージは止められない。
#
# 採用しない場合はこのファイルを削除し、settings.example.json の該当エントリも消す。

input=$(cat)

# コマンド文字列の取り出し。JSON を読めない場合は生の入力をそのまま見る (フェイルクローズ)。
cmd=$(printf '%s' "$input" | python -c "
import json, sys
try:
    print(json.load(sys.stdin).get('tool_input', {}).get('command', ''))
except Exception:
    sys.exit(1)
" 2>/dev/null) || cmd="$input"

[[ "$cmd" == *"gh pr merge"* ]] || exit 0

repo_root=$(git rev-parse --show-toplevel 2>/dev/null) || exit 0
script="$repo_root/review/agy_review.py"
# review/ を採用していないプロジェクトでは何もしない。
[[ -f "$script" ]] || exit 0

# PR 番号は必須。現在のブランチから引かない — hook はセッションの作業ディレクトリ
# (メインツリー = main) で動くため、`cd <worktree> && gh pr merge` でも worktree の
# ブランチは見えず、別の PR を判定するか gh の分かりにくいエラーで止まる。
if [[ "$cmd" =~ gh[[:space:]]+pr[[:space:]]+merge[[:space:]]+#?([0-9]+) ]]; then
    pr="${BASH_REMATCH[1]}"
else
    echo "gh pr merge に PR 番号が指定されていないため、マージを差し止めました。" >&2
    echo "agy レビューの記録を PR 番号で確認するので、\`gh pr merge <PR番号> --merge\` の形で再実行してください。" >&2
    exit 2
fi

# `--repo` / `-R` で別のリポジトリを指すマージは、そのリポジトリの PR として判定する。
# 渡さないと、別リポジトリの PR 番号をこのリポジトリの PR として引いて止まる (#14)。
# 判定を外さない (通さない) のは、レビューを必須にする方針のため。
# 形は gh が受け付けるもの: `--repo x` / `--repo=x` / `-R x` / `-Rx`。
repo_args=()
if [[ "$cmd" =~ (^|[[:space:]])(--repo[=[:space:]]+|-R[=[:space:]]*)([^[:space:]]+) ]]; then
    repo="${BASH_REMATCH[3]//[\"\']/}"
    # `owner/name`、または GitHub Enterprise の `HOST/owner/name`。それ以外は python へ渡さず止める。
    if [[ ! "$repo" =~ ^[A-Za-z0-9_.-]+(/[A-Za-z0-9_.-]+){1,2}$ ]]; then
        echo "--repo の値を解釈できないため、マージを差し止めました。\`--repo <owner>/<name>\` の形で再実行してください。" >&2
        exit 2
    fi
    repo_args=(--repo "$repo")
fi

# 判定できない (gh の失敗など) 場合も止める。exit 2 以外は Claude Code がブロックとして扱わない。
if ! python "$script" --check --pr "$pr" "${repo_args[@]}" >&2; then
    echo "agy レビューの確認が取れないため、マージを差し止めました (手順: agy-review スキル)。" >&2
    exit 2
fi
exit 0
