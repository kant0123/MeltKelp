---
type: operation
summary: MeltKelp をビルドして手元の PC に入れ直し、実機で変換を確かめる手順。ローカルドライブで完結させる理由と、Chrome で Meltype IME が動かない件(ESET と署名)
updated: 2026-10-17
related: [overview, engine-base-choice, upstream-update-stop]
---

# ビルドと入れ直し・実機での確認

入力の挙動に関わる変更(`src/`・`native/`・`packaging/`・`dictionaries/`)をしたとき、上流を取り込んだときに行う。
CI は実際のアプリでの入力を見ないので、この確認は省略できない。上流の手順は [DEVELOPMENT.md](../../docs/DEVELOPMENT.md)。

## 前提

- Windows 11、.NET 10 SDK、Visual Studio Build Tools の「C++ によるデスクトップ開発」(Meltype IME の DLL を作る)
- Mozc ヘルパーも作るなら、Visual Studio 2022(C++ ATL を含む)・Python 3.12 以降・Bazelisk・Git
  (`native/mozc/README.md`。初回は 30〜40 分)
- **リポジトリ・worktree・Mozc のソースをローカルドライブ(C:)に置く**(下の「落とし穴」)

## 手順

1. Mozc ヘルパーを作る(`native/mozc` を変えていなくて、前に作った `native/mozc/bin` があればコピーしてよい。
   `MOZC_COMMIT` が同じであること)。

   ```powershell
   .\native\mozc\Build-MozcHelper.ps1 -MozcSource C:\mozc -Python python -Bazelisk C:\tools\bazelisk.exe
   ```

2. 配布と同じ形の app フォルダーを作る。`dist\Meltype\` に app・`Install.cmd` ができる。

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\Build-Package.ps1
   ```

   自己診断(`Meltype.exe --selftest`)が通らないと止まる。Mozc ヘルパーが無いと警告を出して Microsoft IME だけで作る。
   **Meltype IME の DLL は毎回 `native/tip/build.ps1` で自前ビルドしたもの**が入る(署名は無い)。

3. 入れ直す。`dist\Meltype\Install.cmd` を実行する。動いている Meltype を止め、`%LOCALAPPDATA%\Programs\Meltype` に
   コピーし、Meltype IME を登録する(管理者権限の確認が出る)。公式の Meltype が入っていれば上書きする(置き換え前提)。
   開発用の `Install-Meltype.ps1` はリポジトリの中の `app-build\` から動かすので、worktree を消すと壊れる。確認には使わない。

4. **上流の更新が止まっているか確かめる**(上流の取り込みの後は必ず)。トレイのアイコン →「更新」に「自動で更新する」が無く、
   「今すぐ更新を確認」を押すと止めている旨が出ること。出なければ上流のリリースで公式版に戻される([[upstream-update-stop]])。

5. 実機で確かめる。**メモ帳と Chrome の両方で、2 つの動作モードの両方を試す。**
   - Meltype IME: <kbd>Win</kbd> + <kbd>Space</kbd> で「Meltype」を選ぶ。打った文字が入力欄に下線付きで入り、候補が入力位置の下に出る
   - Meltype キーボード: トレイのアイコンを右クリック →「Meltype キーボード (変換ボックスで入力)」。変換ボックスで入力し、
     Enter で確定した文字列が入力欄に入る
   - 例文は自分で用意したものを使う。並べ替え(#2)の確認なら、文脈「劇団の千秋楽の」+ こうえん → 公演 など
   - **成功の判定:** 4 通り(メモ帳 / Chrome × 2 モード)で変換・確定でき、期待どおりの候補が出る。Chrome で Meltype IME が
     読み込まれないときは、下の ESET の件かを切り分けて PR 本文に書く

6. 速さを比べるとき(変換エンジンの経路を触ったとき)は、変更の前後で `--perf` を動かして比べる
   (`tools/performance/README.md`)。

   ```powershell
   dotnet run -c Release --project src/Meltype.Core.Tests -- --perf before.json
   dotnet run -c Release --project src/Meltype.Core.Tests -- --perf-compare before.json after.json
   ```

## 落とし穴

### Chrome で自前ビルドの IME が読み込まれない(ESET)

出典: imev の `wiki/operations/mozc-build-install.md`(imev #14・#25、2026-10-04〜05)。imev では Mozc の TIP DLL で起きた。
**MeltKelp の Meltype IME の DLL で同じことが起きるかは、まだ確かめていない**(起きると見ている。理由は下)。

- ESET の「すべてのブラウザーを保護」(詳細設定 → 保護 → ブラウザーの保護 → バンキングとブラウジング保護)が有効だと、
  保護された Chrome は信頼できない DLL を読み込まない。自前ビルドの TIP DLL はこれに当たり、Chrome でだけ IME が効かない
  (半角/全角に反応しない・Microsoft IME に切り替わる)。メモ帳などほかのアプリでは動く。
- **自己署名では回避できなかった。** 自己署名の証明書で DLL に署名しても、証明書を `LocalMachine\Root` と `TrustedPublisher` に
  入れて Windows 上の署名状態を `Valid` にしても、Chrome では読み込まれなかった。ESET は Windows の信頼ストアを見ていない。
- Microsoft 以外の IME が一律に止まるわけではない。Google 日本語入力(Google の署名)は同じ ESET 環境で動くという報告がある
  (google/mozc discussion #805)。公的な CA の署名によるのか、ESET の評判(広く出回っているハッシュ)によるのかは切り分けていない。
- 回避は「すべてのブラウザーを保護」を無効にして Chrome を起動し直すこと。imev では設定直後の 1 回目の再起動では直らず、
  その後の再起動で直った(差の原因は未確認)。DLL 単位の除外設定は ESET に無い。
- 切り分け: `chrome://conflicts` で TIP の DLL が「Process types: None」なら、IME として列挙されているがどのプロセスにも
  読み込まれていない。`--user-data-dir=<一時フォルダー>` で別のインスタンスの Chrome を起動すると、普段のプロフィールを
  壊さずに試せる。Chrome 自身の外部 DLL のブロックは登録済みの IME を対象外にしていて、原因ではなかった。
- **MeltKelp がこれに対して取っている方針:** Meltype IME の DLL(`native/tip/`)とパイプの形式を上流と同じに保つ。上流が
  将来、公的な CA で署名した DLL を出せば、`%LOCALAPPDATA%\Programs\Meltype\tip\` の DLL をそれに差し替えるだけで
  済む可能性がある(未検証。今の上流のリリースは署名していない)。`Build-Package.ps1` は毎回 DLL を自前ビルドするので、
  差し替えるならその後で行う。
- Meltype キーボードは TSF の DLL を使わないので、Chrome でも動くはず(未確認)。

### ネットワークドライブ・UNC パスに置かない

出典: imev の `wiki/operations/mozc-build-install.md`。

- imev では、Mozc のソースを UNC パス・ネットワークドライブに置くと Qt の configure が通らなかった。Meltype の Mozc ヘルパーは
  Qt を使わないので同じ場所で落ちるとは限らないが、Bazel もネットワークドライブとの相性が悪い。
- **管理者で起動した PowerShell からは、ユーザーが割り当てたネットワークドライブ(G: など)が見えない。** 管理者で実行する
  スクリプト(Meltype IME の登録)をネットワークドライブ上から動かすと、何も出力せずに失敗した。
- MeltKelp は最初から `C:\Users\kntat\Documents\Git\MeltKelp` に置き、worktree も同じ階層の `MeltKelp-<作業名>` に作る。

### Git Bash から Bazel を呼ぶとターゲット名がパスに変換される

imev で踏んだ。`//...` が MSYS にパス変換されるので、`MSYS_NO_PATHCONV=1` を付ける。`Build-MozcHelper.ps1` は PowerShell から呼ぶ。

## 実施記録

| 日付 | 実施者 | 結果 |
| --- | --- | --- |
| — | — | MeltKelp ではまだ実施していない(テンプレート適用の PR は入力の挙動を変えていない) |
