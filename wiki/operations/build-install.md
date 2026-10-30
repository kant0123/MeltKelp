---
type: operation
summary: MeltKelp をビルドして手元の PC に入れ直し、実機で変換を確かめる手順。ローカルドライブで完結させる理由と、Chrome で Meltype IME が動かない件(ESET と署名)
updated: 2026-10-30
related: [overview, engine-base-choice, upstream-update-stop, zenz-rerank]
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

   zenz の並べ替え([[zenz-rerank]])を確かめるなら、ランタイムも置く(初回だけ。`Install.cmd` はこのフォルダーに触らない)。
   置いた後に Meltype を起動し直し、トレイの「ログ / 判定理由」に「zenz: 読み込みました」が出ることを確かめる。

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\Install-ZenzRuntime.ps1   # imev の一式があれば -Source $env:LOCALAPPDATA\MozcRerank
   ```

   GPU で採点するなら `-Gpu` を付けてもう 1 回実行する(Vulkan 版を `zenz\vulkan` に置く。CPU 版は `zenz\cpu`)。両方置けば、設定
   「zenz を GPU で動かす」で選べる(起動し直すと効く。[[zenz-rerank]] の「GPU(Vulkan)で採点する」)。動いている Meltype が DLL を掴んでいると
   上書きできないので、置き直すときはトレイから終了してから実行し、その後で `Install.cmd` か Meltype の起動で読み込ませる。
   GPU で動いているかは、ログの「zenz: 読み込みました (GPU (Vulkan)・…)」か、`tasklist /M ggml-vulkan.dll` に Meltype.exe が出るかで分かる。

4. **上流の更新が止まっているか確かめる**(上流の取り込みの後は必ず)。トレイのアイコン →「更新」に「自動で更新する」が無く、
   「今すぐ更新を確認」を押すと止めている旨が出ること。出なければ上流のリリースで公式版に戻される([[upstream-update-stop]])。

5. 実機で確かめる。**メモ帳と Chrome の両方で、2 つの動作モードの両方を試す。**
   **確かめるアプリは入れ直した後に起動し直す**(起動中のアプリは古い Meltype IME の DLL を使い続ける。下の「入れ直しても起動中のアプリは古い DLL のまま」)。
   - Meltype IME: <kbd>Win</kbd> + <kbd>Space</kbd> で「Meltype」を選ぶ。打った文字が入力欄に下線付きで入り、候補が入力位置の下に出る
   - Meltype キーボード: トレイのアイコンを右クリック →「Meltype キーボード (変換ボックスで入力)」(アイコンは通知領域の「^」の中に
     隠れていることがある。見えている「あ」は Meltype IME の入力インジケーターで、右クリックしてもこのメニューは出ない)。変換ボックスで入力し、
     Enter で確定した文字列が入力欄に入る
   - 例文は自分で用意したものを使う。並べ替え(#2)の確認なら、文脈「劇団の千秋楽の」+ こうえん → 公演 など。
     Mozc が学習した語は zenz が動かせることがあるので([[zenz-rerank]] の「学習との順序」)、未学習の語で確かめる
   - **成功の判定:** 4 通り(メモ帳 / Chrome × 2 モード)で変換・確定でき、期待どおりの候補が出る。Chrome で Meltype IME が
     読み込まれないときは、下の ESET の件かを切り分けて PR 本文に書く

6. 速さを比べるとき(変換エンジンの経路を触ったとき)は、変更の前後で `--perf` を動かして比べる
   (`tools/performance/README.md`)。

   ```powershell
   dotnet run -c Release --project src/Meltype.Core.Tests -- --perf before.json
   dotnet run -c Release --project src/Meltype.Core.Tests -- --perf-compare before.json after.json
   ```

   zenz のランタイムがあれば `--perf` も zenz で包んで測る(`MELTKELP_ZENZ=0` で包まない)。既定の例文は英単語まじりの短い文で
   zenz がほとんど採点しないので、zenz の分を測るなら `tools/zenz-rerank-cases.tsv` を例文のファイルに渡す。

## 落とし穴

### パッケージの削減で zenz の読み込みに要るアセンブリが消える

- `Build-Package.ps1` の「2. 読み込まれなかった大きなアセンブリを削る」は、**自己診断(`--selftest`)で読み込まれなかった** 512KB 超の
  アセンブリを消す。自己診断は zenz を読み込まないので、zenz だけが使うアセンブリはここで消える。
- 2026-10-18 に踏んだ(#40)。`System.Security.Cryptography.dll`(`LlamaZenzScorer.Load()` が llama.dll の版を SHA-256 で確かめる)が消え、
  `Load()` の JIT で `FileNotFoundException` になり、トレイの「LLM (zenz)」が「読み込み中」のまま、zenz の DLL が 1 つも読み込まれなかった
  (CPU 版・Vulkan 版とも)。同じ `Meltype.Core.dll` を pwsh から読み込んで `WaitUntilReady()` を呼ぶと読み込めたので、パッケージの側だと分かった。
- 対処: 削る対象から除外する(`System.Reflection.Metadata.dll` と同じ形の 1 行)。除外が消えると `ZenzRerankTests.BuildPackage_KeepsCryptographyForZenz` が落ちる。
  `Load()` に入る前の失敗でも「読み込めませんでした」になるよう、`EnsureLoading()` の読み込みスレッドの finally でも終わった印を立てる。
- zenz の部品に新しく .NET のアセンブリ(512KB 超)を使わせたら、同じように除外が要る。入れ直した後はトレイの状態が「有効」になることまで見る。

### 入れ直しても起動中のアプリは古い DLL のまま

- `Install.cmd`・`Install-Meltype.ps1`・`packaging/meltype-ime.ps1` で Meltype IME を入れ直すと、使用中の `MeltypeTip.dll` は
  `MeltypeTip.dll.<日時>.old` に改名され、新しい DLL が置かれる(`native/tip/Register-Tip.ps1`)。だが**起動中のアプリは、
  起動時に読み込んだ古い DLL(改名後の `.old`)を使い続ける。** 新しい DLL を読み込むのは、その後に起動したアプリだけ。
- 2026-10-18 に踏んだ(#37)。10/17 から起動しっぱなしのサクラエディタ(32 bit。`tip\x86` を読み込む)が 10/11 版の DLL のままで、
  10/12 に入った「候補の一覧の上に打ったキーを出す」(57b9d95)が出ず、後から起動したメモ帳では出た。コードの不具合に見えるので注意。
- 確かめ方: `Get-Process <名前> | Select-Object StartTime` で起動時刻を見て、`tip\x64`・`tip\x86` の `MeltypeTip.dll` の更新日時と
  `.old` の日時と比べる。起動時刻が今の DLL より前なら古い DLL を使っている。**プロセスのモジュール一覧のパスは改名後も元の名前
  (`MeltypeTip.dll`)のまま出るので、パスでは見分けられない。**
- 対処: 入れ直した後は、確かめるアプリ(メモ帳・Chrome・32 bit のアプリ)を終了して起動し直す。

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

### `Meltype.Tests` のクリップボードのテストが時々落ちる

#13。`WindowsTests.CopyClipboard_KeepsContentAfterClipboardChanges` と `ClipboardSequence_ChangesOnlyWhenContentChanges`
(`src/Meltype.Tests/WindowsTests.cs`)が、変更と無関係に「要求されたクリップボード操作に成功しませんでした」(`0x800401D0`、
`CLIPBRD_E_CANT_OPEN`)で落ちる。

- **手元(Windows 11、クリップボード履歴が ON)では、前者を単独で流すと 30 回中 12 回落ちた。** 全体を流すと 1 回落ちる程度に見えるのは、
  落ちるのが実行の一部だから。
- **原因はクリップボード履歴のサービス(`cbdhsvc`、`svchost` の中)。** テストが入れ直すたびに読みに行く。掴んでいる時間が数秒から 20 秒以上に
  なることがあり、上流の「500ms 間隔で 5 回」(約 2 秒)では足りなかった。掴んでいたプロセスは `GetOpenClipboardWindow` を 20ms 間隔で
  引いて突き止めた(`svchost` 445 回、ほかは各 1〜6 回)。
- **待ち方も効いた。** 待ちが `Thread.Sleep` だと STA スレッドがメッセージを処理せず、掴んだ側がこちらのデータ オブジェクトを OLE で
  呼び出したまま返事を待つ形になり、開けない状態が 20 秒以上続いた。`WindowsTests.PumpSleep()`(`Application.DoEvents()` を回しながら待つ)に
  替えると 40 回とも通った(落ちる試行はあるが、再試行 2 回以内で直った)。この因果は、症状の消え方と呼び出しの形からの推定で、
  `cbdhsvc` 側の動きは見ていない。
- 再試行は回数(5 回)ではなく時間(20 秒)で区切っている。クリップボード履歴を OFF にして比べることはしていない(システム設定を変えるため)。
- 製品側(`CompositionService.RestoreClipboard`)は Windows のメッセージ ループで動くので、この待ち方の問題は受けない。

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
| 2026-10-30 | ユーザー + エージェント | PR #48(#46。上流 v1.4.0 の取り込み)。Mozc ヘルパーは `MOZC_COMMIT` が同じなので前のものを流用。`Build-Package.ps1` → `Install.cmd` で入れ直し、メモ帳・Chrome × 2 モードの 4 通りで変換でき、更新が止まったままなのも確かめた(ユーザーの確認)。以前から `APIno` が「APIの」にならない件は #49(判定だけの再現では「APIの」になる) |
| 2026-10-18 | ユーザー + エージェント | PR #26(#2)。Mozc ヘルパーは入っていた MeltKelp のもの(公式 1.2.1 由来)を流用、zenz のランタイムは `Install-ZenzRuntime.ps1 -Source`(imev の一式)。メモ帳・Chrome × 2 モードの 4 通りで、劇団の千秋楽の + こうえん → 公演・著名な研究者の → 講演・街の → 公園・「劇団の千秋楽の」を確定してから こうえん → 公演。打鍵のもたつきは気にならなかった。1 回目は Install.cmd が効いておらず古い版のままだった(DLL の日付で気付いた)。2 回目は前の行が文脈に入って 街の → 公演 になり、直した |
| 2026-10-17 | ユーザー + エージェント | PR #9(#3)。Mozc ヘルパーは公式 1.2.1 のものを流用。メモ帳・Chrome × 2 モードの 4 通りで変換できた(Chrome でも Meltype IME が動いた)。トレイの「更新」に「自動で更新する」が無く「今すぐ更新を確認」で止めている旨が出る・設定に「9. 更新」が無い・`ready.txt` を置いて起動し直しても適用されない |
