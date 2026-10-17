# MeltKelp(とろろ昆布)

**半角/全角 キーを押さずに打てる日本語入力 Meltype の、個人用フォーク。**
漢字変換の候補を、小型の言語モデル zenz で文脈つきに並べ替えることを目指しています。

対応 OS は Windows 10 / 11(64bit)。ライセンスは GPL-3.0。

## これは何か

[Meltype](https://github.com/yksr-melt/Meltype)(yksr-melt 作、GPL-3.0)は、ローマ字で打つだけで日本語か英語かを見分けて、
その場で切り替える Windows 用の日本語入力です。MeltKelp はその個人用フォークで、**手元の PC に自分でビルドして入れる**ためのものです。
リリースは配布しません。

| | 状態 |
|---|---|
| Meltype の機能(英日の自動判定・変換・学習・アプリごとの設定) | 上流のまま使える |
| zenz による漢字変換候補の文脈つき並べ替え | **未実装**([#2](https://github.com/kant0123/MeltKelp/issues/2)) |
| 上流 Meltype のリリースによる自動更新 | 止めてある(公式版で MeltKelp が上書きされないように) |
| 上流の Issue 画面を開く報告の導線 | 止めてある(トレイの「不具合の報告・提案」などを出さない) |

並べ替えが目指すのは、たとえば文脈「劇団の千秋楽の」+ こうえん → **公演**、「街の」+ こうえん → **公園** のような選び分けです。
上流がエンジンに渡す文脈は短い(キャレットの前の同じ文の日本語・最大 10 文字)ので、それを zenz で補います。
前身の imev(Mozc に zenz を載せた個人用 IME)で確かめた方式を移します。

### 上流との関係

- **公式の Meltype を置き換える前提**です。インストール先(`%LOCALAPPDATA%\Programs\Meltype`)・データフォルダー
  (`%LOCALAPPDATA%\Meltype`)・TSF の識別子・パイプ名は上流と同じなので、公式と並べて入れられません。設定と学習データはそのまま引き継ぎます。
- 上流の全履歴を持つ独立したリポジトリで、GitHub のフォーク機能は使っていません。上流には何も出しません(push・PR・Issue)。
  Issue と PR はこのリポジトリ(`kant0123/MeltKelp`)に立てます。
- 上流との差分は最小にして、取り込みは `git merge upstream/main` で行います。理由と経緯は [wiki/overview.md](wiki/overview.md)。

## 入れ方

配布物は無いので、ビルドして入れます。手順の全文と落とし穴(Mozc ヘルパー、ローカルドライブに置く理由、Chrome と ESET の件)は
[wiki/operations/build-install.md](wiki/operations/build-install.md) にあります。

必要なもの: Windows 11、.NET 10 SDK、Visual Studio Build Tools の「C++ によるデスクトップ開発」(Meltype IME の DLL を作る)。
Mozc の変換ヘルパーも作るなら、Visual Studio 2022・Python 3.12 以降・Bazelisk・Git も要ります(初回は 30〜40 分)。
**リポジトリはローカルドライブ(C:)に置いてください。** ネットワークドライブでは Mozc のビルドが通らず、管理者の PowerShell からも見えません。

```powershell
# Mozc の変換ヘルパー(省略すると Microsoft IME だけで動く)
.\native\mozc\Build-MozcHelper.ps1 -MozcSource C:\mozc -Python python -Bazelisk C:\tools\bazelisk.exe

# 配布と同じ形の app フォルダー(dist\Meltype\)を作る
powershell -ExecutionPolicy Bypass -File .\Build-Package.ps1

# 入れ直す(Meltype IME の登録で管理者権限の確認が出る)
.\dist\Meltype\Install.cmd
```

入ると、トレイに「あ」が出ます。トレイのアイコン →「更新」に「自動で更新する」が無いことも確かめてください(あると、上流のリリースで公式版に戻されます)。

> **Chrome で Meltype IME が動かないことがあります。** 自前ビルドの TIP DLL には署名が無く、ESET の「すべてのブラウザーを保護」が
> 有効だと Chrome が読み込みません(メモ帳などでは動く)。詳細と回避は [build-install の落とし穴](wiki/operations/build-install.md)。
> Meltype キーボードは DLL を使わないので、Chrome でも使えるはずです。

アンインストールは、トレイのアイコンを右クリックして「アンインストール...」か、Windows の「設定」→「アプリ」から。

## 使い方

あとは、いつもどおりローマ字で打つだけです。

```
kyouhagoogledekensaku    →  今日はgoogleで検索
ashitanomeetingwotsuika  →  明日のmeetingを追加
I want to go to the park →  I want to go to the park
```

動作モードは 2 つあります。トレイのアイコンを右クリックして切り替えます。

- **Meltype IME**: 文字は入力欄に直接入り、候補は入力位置の下に出る。ほかの IME との切り替えは <kbd>Win</kbd> + <kbd>Space</kbd>
- **Meltype キーボード**: カーソルの下に変換ボックスが出る

| キー | |
|---|---|
| <kbd>Enter</kbd> | 確定 |
| <kbd>Space</kbd> | 候補を出して選ぶ(4 文字以上は打つそばから漢字になる。短い語は Space で変換) |
| <kbd>F7</kbd> / <kbd>F10</kbd> | カタカナ / 英字 |
| <kbd>半角/全角</kbd> | 英数と日本語の切り替え |
| <kbd>Ctrl</kbd> + <kbd>半角/全角</kbd> | Meltype を一時停止 |

<kbd>F10</kbd> で英字に直した語は、次から英字で出ます。よく使う言葉は、トレイの「ユーザー辞書...」から登録できます。
VS Code やターミナルでは英数が基本で、コメントと文字列の中だけ日本語になります。
キー操作・アプリごとの設定の全部は、上流の文書 [docs/USAGE.md](docs/USAGE.md) にあります(MeltKelp では書き換えません)。

## プライバシー

打った文字は PC の外へ送りません。判定も変換も PC の中で完結します。保存するのは `%LOCALAPPDATA%\Meltype` の設定・学習データ・
ユーザー辞書(と、ON にしたときのログ)だけです。

上流の Meltype が行う通信は、自動更新の確認と、自分で開いた報告のフォームだけでした。MeltKelp ではどちらも止めてあります。
ログ(`meltype.log`)は、設定「ログに入力した文字を残す」が OFF でも判定した語の先頭の数文字を含むので、貼り付けるときは気をつけてください。
セキュリティの方針は上流の [SECURITY.md](SECURITY.md) にあります。

## 開発

| すること | 手順 |
|---|---|
| 全体像・設計判断を知る | [wiki/overview.md](wiki/overview.md) → [wiki/index.md](wiki/index.md) |
| 作業の進め方(worktree・Issue・PR・レビュー) | [CLAUDE.md](CLAUDE.md)、[docs/development_workflow.md](docs/development_workflow.md) |
| 上流の仕組み | [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)、[docs/TSF_DESIGN.md](docs/TSF_DESIGN.md)(どちらも上流の文書) |
| 上流の更新を取り込む | `upstream-sync` スキル([SKILL.md](.claude/skills/upstream-sync/SKILL.md)) |

```
src/Meltype.Core/   OS に依存しない判定・変換の流れ・学習
src/Meltype/        Windows 版の本体(Meltype.exe。MeltKelp が手を入れるのはここだけ)
native/tip/         Meltype IME(TSF の DLL)。上流と同じ形を保つ
native/mozc/        Mozc の変換ヘルパー
wiki/               MeltKelp の知識(今どうなっているか・なぜそうしたか)
```

`mac/`・`linux/` は上流のもので、MeltKelp では使いません。テスト:

```powershell
dotnet run --project src/Meltype.Tests -c Release          # Windows。共通部分のテストも含む
python -m pytest review/tests -q                           # レビュー基盤
node --test .claude/hooks/test/*.test.mjs scripts/test/*.test.mjs
```

変換の挙動を変えたら、CI だけでなく**メモ帳と Chrome で、Meltype IME と Meltype キーボードの両方**を実機で確かめます。
CI は TSF への登録と実際のアプリでの入力を見ません。

## ライセンスと謝辞

[GNU GPL v3.0](LICENSE) のままです。MeltKelp は [Meltype](https://github.com/yksr-melt/Meltype)(Copyright (C) 2026 雪代 / Yukishiro)の
フォークで、土台のすべては上流の作者とその協力者のものです。ロゴは [@Crysta1221](https://github.com/Crysta1221) さん、
Meltype IME は [@lnkiai](https://github.com/lnkiai) さん、Meltype IME の登録(インストーラー)は [@MuNeNiCK](https://github.com/MuNeNiCK) さんの
お仕事です。テスト版や寄付で支えてくださった方々の一覧は[上流の README](https://github.com/yksr-melt/Meltype#readme) にあります。

<details>
<summary>著作権表示・同梱物</summary>

```
Meltype
Copyright (C) 2026 雪代 / Yukishiro (@yksr_melt / @yksr-melt)

This program is free software: you can redistribute it and/or modify it under the terms of the
GNU General Public License as published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without
even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
General Public License for more details.
```

ソースファイルには `SPDX-License-Identifier: GPL-3.0-or-later` を付けています。
同梱している .NET ランタイム (MIT)、Mozc (BSD-3-Clause) と、使っている Windows の機能については
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) を見てください。
ロゴ ([docs/meltype.jpg](docs/meltype.jpg)・[docs/images/logo.png](docs/images/logo.png)) は [@Crysta1221](https://github.com/Crysta1221) さんに描いていただきました。

</details>
