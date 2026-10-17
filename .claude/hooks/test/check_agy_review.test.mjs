// .claude/hooks/check_agy_review.sh の回帰テスト。
//
//   node --test .claude/hooks/test/check_agy_review.test.mjs
//
// 一時ディレクトリに「review/agy_review.py の代役を持つ git リポジトリ」を組み立て、hook を実プロセスとして
// 起動する。代役は渡された引数を記録するだけで、hook が `--repo` を判定に渡すかを見る。
// bash と python が要る(CI の Linux には入っている)。

import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, test } from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';

const HOOK = fileURLToPath(new URL('../check_agy_review.sh', import.meta.url));

const sandbox = mkdtempSync(join(tmpdir(), 'check-agy-review-test-'));
after(() => rmSync(sandbox, { recursive: true, force: true }));

const root = join(sandbox, 'repo');
mkdirSync(join(root, 'review'), { recursive: true });
const record = join(sandbox, 'args.json');
writeFileSync(join(root, 'review', 'agy_review.py'), [
  'import json, os, sys',
  `open(${JSON.stringify(record)}, "w").write(json.dumps(sys.argv[1:]))`,
  'sys.exit(int(os.environ.get("STUB_EXIT", "0")))',
  '',
].join('\n'));
const git = spawnSync('git', ['init', '-q'], { cwd: root });
assert.equal(git.status, 0, 'git init');

/** hook を起動し、{ status, stderr, args }(代役 agy_review.py が受け取った引数。呼ばれなければ null)を返す。 */
function run(command, env = {}) {
  rmSync(record, { force: true });
  const r = spawnSync('bash', [HOOK], {
    cwd: root,
    input: JSON.stringify({ tool_input: { command } }),
    encoding: 'utf8',
    env: { ...process.env, ...env },
  });
  return {
    status: r.status,
    stderr: r.stderr,
    args: existsSync(record) ? JSON.parse(readFileSync(record, 'utf8')) : null,
  };
}

test('マージ以外のコマンドは見ない', () => {
  const r = run('git status');
  assert.equal(r.status, 0);
  assert.equal(r.args, null);
});

test('--repo 無しは PR 番号だけを渡す', () => {
  const r = run('gh pr merge 12 --merge');
  assert.equal(r.status, 0);
  assert.deepEqual(r.args, ['--check', '--pr', '12']);
});

test('PR 番号が無ければ止める', () => {
  const r = run('gh pr merge --merge');
  assert.equal(r.status, 2);
  assert.equal(r.args, null);
});

for (const [label, command] of [
  ['--repo x', 'gh pr merge 50 --repo o/r --merge'],
  ['--repo=x', 'gh pr merge 50 --repo=o/r --merge'],
  ['-R x', 'gh pr merge 50 -R o/r --merge'],
  ['-Rx', 'gh pr merge 50 -Ro/r --merge'],
  ['引用符つき', 'gh pr merge 50 --repo "o/r" --merge'],
  ['末尾', 'gh pr merge 50 --merge --repo o/r'],
]) {
  test(`${label}: 指定したリポジトリの PR として判定する`, () => {
    const r = run(command);
    assert.equal(r.status, 0);
    assert.deepEqual(r.args, ['--check', '--pr', '50', '--repo', 'o/r']);
  });
}

test('GitHub Enterprise の HOST/owner/name も渡す', () => {
  const r = run('gh pr merge 50 --repo ghe.example.com/o/r --merge');
  assert.deepEqual(r.args, ['--check', '--pr', '50', '--repo', 'ghe.example.com/o/r']);
});

test('別リポジトリでも判定は外さない(レビューが無ければ止まる)', () => {
  const r = run('gh pr merge 50 --repo o/r --merge', { STUB_EXIT: '2' });
  assert.equal(r.status, 2);
  assert.match(r.stderr, /差し止め/);
});

test('--repo の値が解釈できなければ判定へ渡さず止める', () => {
  for (const bad of ['--repo --merge', '--repo ;rm', '--repo notaslug', '--repo a/b/c/d']) {
    const r = run(`gh pr merge 50 ${bad}`);
    assert.equal(r.status, 2, bad);
    assert.equal(r.args, null, bad);
  }
});
