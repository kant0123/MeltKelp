---
type: component
summary: zenz で漢字変換の最初の候補を文脈つきに選び直す部品(HybridConverter のデコレーターと llama.cpp の採点器)。差し込み口・文脈の作り方・学習との順序・かなの扱い・ランタイムの置き場所と版の固定・GPU(Vulkan)での採点と CPU との切り替え
updated: 2026-10-30
related: [context-rerank-lessons, engine-base-choice, build-install, overview, recurring-review-findings]
---

# zenz の並べ替え

変換エンジン(`HybridConverter`。Mozc と Microsoft IME)が出した各文節の最初の候補を、キャレットの前の文字と同じ変換の前の文節を
文脈にして、zenz-v3.1-small で選び直す(#2)。候補を足したり消したりはせず、文節の区切りも変えない。`native/tip/` とパイプの
形式には触れていない(`Meltype.exe` の中で完結する)。

## 入口となるファイル

| パス | 役割 |
| --- | --- |
| `src/Meltype.Core/Composition/ZenzRerank.cs:68 ZenzRerankingConverter` | デコレーター本体。`Choose()` が 1 文節分の選び直し、`Candidates()` が ↑↓ の一覧の並べ替え |
| `src/Meltype.Core/Composition/LlamaZenzScorer.cs:327 Rank()` | 採点。imev の `LlmScorer::Impl::Rank()` を C# に移したもの。`Load()` が版の確認と読み込み |
| `src/Meltype.Core/Composition/CompositionController.cs:1744 ConvertJapanese()` | 差し込み口: 変換の直前に `_precedingText` を渡す 1 行 |
| `src/Meltype/Composition/CompositionService.cs:46 CompositionService()` | 差し込み口: コンストラクターの中(125 行目あたり)で `HybridConverter` を包む |
| `scripts/Install-ZenzRuntime.ps1` | ランタイムを `%LOCALAPPDATA%\Meltype\zenz` に置く |

上流のファイルへの変更は、ほかに `Settings`・`CompositionOptions` の `partial`(設定の `ZenzRerank` を別ファイルで足すため)、
`TrayApplicationContext` の 2 行(設定の `ZenzRerank`・`ZenzUseGpu` を渡す)、`Meltype.Core.csproj` の `AllowUnsafeBlocks`、`--perf` の `Performance.cs` の 4 行だけ(zenz で包む・cold で控えを捨てる・計測用のラッパーが文脈を渡す・一覧を zenz のものにする)。

## 上流の Zenzai との関係

上流は v1.4.0 で Zenzai(zenz + llama-server によるかな漢字変換。設定「Zenzai を使う」、初期は OFF)を足した。上流は
`HybridConverter` のコンストラクターに Zenzai を渡し、ON なら読み全体を Zenzai で変換して Mozc の文節に当てはめる。
MeltKelp の並べ替えはその `HybridConverter` を外から包む形のまま載せ直した(#46)ので、**両方 ON だと Zenzai の結果の
各文節の先頭をもう一度選び直す。** この組み合わせは確かめておらず、扱いは #47 で決める。

- `HybridConverter` が実装するインターフェース(`ILearningConverter`・v1.4.0 で足された `IWideContextConverter`・`IStandaloneConverter`)は、
  デコレーターも実装して中身に渡す。Controller が `_converter is ...` で見るので、渡さないと黙って機能が落ちる(#48 の agy レビューで発覚。
  [[recurring-review-findings]] の「包むラッパーが、中身の実装している追加のインターフェースを隠す」)

- 並べ替えを Zenzai に置き換えなかったのは、方式が違うため。Zenzai は読みから文を生成し、自信が低い・遅い(400ms 超)・
  文脈の無い短い読みでは使わない。並べ替えは変換エンジンの候補の中から選ぶだけで、候補を作らない([[engine-base-choice]]・
  [[context-rerank-lessons]])
- 上流の Zenzai は別プロセス(llama-server)、並べ替えは `Meltype.exe` の中の llama.cpp(`LlamaZenzScorer`)。ランタイムの置き場所も別

## 仕組みの要点

- **選び直すのは `ConvertClauses()` の各文節の `Text`。** 最初の Space で出る候補はこれで決まり、`Candidates()`(`MoreCandidates`)は
  `CompositionController.Expand()` が先頭を固定したまま後ろに足すだけなので、`Candidates()` だけ並べ替えても先頭は変わらない。
  `Candidates()` は、同じ読みを最後に採点できたときの文脈の採点で、採点した候補どうしの位置の中で並べ替えるだけ(採点し直さない。
  事前は一覧の中の位置)。その文脈は変換のたびに文節の読みごとに消してから採点し直す。採点できなかった変換(文脈が空・時間切れ・失敗)の後に、
  前の文脈の順で一覧が並ばないようにするため(agy レビューの指摘。[[recurring-review-findings]])。
- **文脈は zenz 用に別に作る。** 上流の文脈(`ConversionContext()`: 同じ文の日本語・最大 10 文字)は使わず、`_precedingText`
  (キャレットの前の確定済みの文字。UI Automation と Meltype IME はどちらも 20 文字までしか読まない)の**最後の改行より後ろ**に、同じ変換の前の文節
  (選び直した後のもの)を足し、**足した後に**末尾 20 文字に切る(`ZenzRerankOptions.MaxContextChars`)。
  変換エンジンは変換ボックスと Meltype IME の入力欄ごとの `MeltypeSession` で共有されるので、Controller が変換のたびに
  `IPrecedingTextReceiver.SetPrecedingText()` で渡す形にした。
- **文脈が空なら採点しない。** キャレットの前に何も無くても、2 番目以降の文節は前の文節が文脈になるので採点する
  (げきだんのこうえん → 劇団の|公演)。
- **採点するのは変換エンジンの上位 5 件**(文節の `Text` を先頭に、`Candidates()` から重ねずに足す)。合成は
  「対数確率 − 0.6 × 元の順位」の大きい順(imev の `-500 × 対数確率 + 300 × 順位` と同じ比)。
- **プロンプト:** `[BOS] U+EE02 文脈 U+EE00 読みのカタカナ U+EE01 候補`(azooKey の `ZenzPromptBuilder` の v3 と同じ)。
  文脈 + 読みの KV を系列 0 に置いて次の採点でも共通の頭を使い回し、候補ごとに `seq_cp` で系列を分けて 1 回の decode で採点する。
  終わったら(途中で失敗しても)分けた系列を消す。
- **時間:** 1 回の変換で採点に使ってよいのは合計 150ms(`ZenzRerankOptions.BudgetMs`)。予算は `SetPrecedingText()`(`ConvertJapanese()` の先頭)
  から数える。`ConvertClauses()` を単位にすると、`CompositionController.ConvertWithContext()` が文脈あり・なしで 2 回呼ぶので予算の 2 倍止まる
  (agy レビューの指摘)。過ぎたら残りの文節は
  変換エンジンの順のまま、締め切りは `llama_set_abort_callback` で decode の途中でも打ち切る。モデルの読み込みは別スレッドで、
  終わるまでは採点しない。読み込みは Meltype の起動時(`HybridConverter` を包んだとき)に始める。最初の変換で始めると、起動直後の
  1 回目の変換が並べ替わらず、実機の確認で「効いていない」と見誤る。読み込みの最後に 1 回試しに採点しておく(最初の decode は計算用のバッファを確保するので遅い)。
- **キャッシュ:** (文脈, 読み, 候補) → 対数確率を**候補ごとに**控え、足りない候補だけを採点する(候補は別々の系列で採点するので、
  まとめ方で値は変わらない。文脈あり・なしで先頭の候補が違っても採点し直さない)。**失敗・時間切れも覚える**(ライブ変換は打つたびに
  変換するので、同じ入力で毎回締め切りまで待たないため)。2048 件で捨てて覚え直す。

## 学習との順序

- **Meltype の確定履歴・ユーザー辞書・文脈の手がかり辞書(`ContextRules`)は、zenz の後で効く。** どれも
  `CompositionController.ConvertWithEngine()` が変換エンジンの結果に `Prefer()` で掛けるので、選び直した変換は zenz が何を言っても
  先頭に残る(`ZenzRerankTests.Controller_PassesPrecedingTextAndLearnedChoiceStaysFirst`)。手がかり辞書(料理 → 箸)も zenz より強い。
- **Mozc の学習で先頭に来た候補は見分けられないので、守れない。** Mozc ヘルパー(`native/mozc/meltype_mozc_helper.cc`)は候補の
  文字列しか返さない。守れないのは Mozc だけが覚えている語(既定のまま確定して強化された語・1 文字の読み・かなで確定した語。
  Meltype の確定履歴は既定から選び直した、読み 2 文字以上の語しか覚えない)。ヘルパーに学習の印を出させる案は、Mozc の
  ビルドのやり直しと `native/mozc/` のやり取りの形式の変更が要るので採っていない。
- **zenz が選んだ候補をそのまま確定すると、Mozc がそれを覚える**(`Learn()` はそのまま `HybridConverter` に渡す)。次からは文脈が
  無くても Mozc がその候補を先に出す。ユーザーが確定したものなので、そのままにしている。

## かなだけの候補も採点する

Issue の時点では imev に倣って外す予定だったが、**採点に入れた。** imev で読みを写したカタカナ(ゲキダン)が勝っていたのは、
imev のプロンプトのタグの誤り(U+E200〜U+E202。正しくは U+EE00〜U+EE02)によるものだった。正しいタグでの実測
(`ZenzScorerTests.RealRuntime_KanaCandidates`):

| 文脈 + 読み | 候補の対数確率 |
| --- | --- |
| 有名な + げきだん | 劇団 -0.01 / ゲキダン -13.01 / げきだん -11.70 |
| 週末に買い物を + したい | したい -0.00 / 死体 -17.28 / 肢体 -19.26 |
| 宿題を + やって | やって -0.00 / 遣って -8.57 / ヤッテ -12.10 |
| 明日の + てすと | テスト -0.00 / てすと -8.88 |

かなを外すと、変換エンジンが漢字にした語尾・補助動詞(したい → 死体。imev #31)を戻せない。入れれば戻せて、写しのカタカナも勝たない。

## ランタイム

- 置き場所は `%LOCALAPPDATA%\Meltype\zenz`。モデル(`ggml-model-Q5_K_M.gguf`)は直下に 1 つ、llama.dll・ggml*.dll は CPU 版を `cpu\`、
  Vulkan 版を `vulkan\` に置く(#31。#31 より前の直下の DLL も読み込むが、スクリプトは消す)。上流の配布物とインストール先は変えない。
  データフォルダーのバックアップ(`Config/Backup.cs`)は決まったファイルしか入れないので、74MB のモデルは入らない。
- **llama.dll は azooKey/llama.cpp の b4846 の CPU 版(`llama-b4846-bin-win-avx-x64.zip`)と Vulkan 版(`…-win-vulkan-x64.zip`)のものしか
  読み込まない**(SHA-256 で確かめる。`LlamaZenzScorer.LlamaBuilds`)。`llama_model_params` などの構造体を値で受け渡すので、版が違うと形が
  合わずに `Meltype.exe` ごと落ちうる。合わなければ読み込まずに並べ替えをしない(フェイルオープン)。版を上げるなら、ヘッダの構造体を
  突き合わせてからコードとスクリプトのハッシュを両方直す(食い違うと `ZenzRerankTests.InstallScript_PlacesOnlyLlamaBuildsTheScorerLoads` が落ちる)。
  b4846 の API は `llama_kv_cache_*`(新しい版では `llama_memory_*` に変わっている)。
- 依存する ggml*.dll は、既定の検索順で別の場所の DLL を拾わないよう、同じフォルダーから先に読み込む。ggml-rpc.dll が欠けると
  llama.dll が読み込めない(imev で踏んだ)。
- llama.cpp は読み込みの様子を標準エラーに書く(モデルの情報だけで、入力は書かない)。`Meltype.exe` では捨てられる。

## GPU(Vulkan)で採点する

#29・#31。`Install-ZenzRuntime.ps1 -Gpu` で Vulkan 版を `zenz\vulkan` に、`-Gpu` なしで CPU 版を `zenz\cpu` に置く。どちらを読み込むかは
設定「zenz を GPU で動かす」(`ZenzUseGpu`、既定 ON)で選ぶ(`LlamaZenzScorer.LibraryDirectory()`)。ON なら Vulkan 版(無ければ CPU 版)で、
GPU が見つかればモデルを全部 GPU に載せる。OFF なら CPU 版だけを使い、Vulkan 版には触らない(CPU 版が無ければ並べ替えをしない)。

- **2 つ並べて置く理由:** Vulkan 版 1 つで `n_gpu_layers` を 0 にする案は、その場で切り替えられるが、0 にしてもまとまった量の計算は GPU に
  回り、Vulkan の初期化とドライバーのメモリ(約 +160MB)も残るので、「GPU を使わない」にならない。ユーザーの判断で並べて置く方にした。
- **切り替えは起動し直したときに効く。** CPU 版と Vulkan 版は同じ名前の DLL(llama.dll・ggml.dll など)なので、1 つのプロセスで読み替えると、
  後から読み込んだ llama.dll の依存が先に読み込んだ ggml.dll に結び付く。設定は読み込むとき(起動時)に 1 回だけ見る。設定の説明にもそう書いた。

- **Vulkan だけにした理由:** 手元の GPU は AMD Radeon RX 9060 XT(RDNA4・gfx1200)。b4846 の HIP 版は gfx1030/1100/1101 向けしか無く、
  CUDA は NVIDIA 専用。Vulkan 版は同じコミットなので、構造体の形を確かめ直さずに済んだ。
- **GPU が無いとき:** Vulkan 版でも、ggml に GPU の装置が無ければ(`HasGpu()`)CPU で読み込む。GPU に載せられなければ CPU で読み込み直す。
  Vulkan のドライバー(`vulkan-1.dll`)自体が無い PC では `ggml.dll` が読み込めず、並べ替えをしない(同じ理由で、同じプロセスで CPU 版に
  読み替えられない)。設定を OFF にして起動し直せば CPU 版で動く。
- **締め切りで打ち切れない。** abort callback は CPU のバックエンドでしか効かないので、GPU では decode を途中で止められない。
  1 回の decode が 10〜30ms なので、150ms の予算は decode の前の確認で守れている。
- **初めての形の decode が 200ms 前後かかる。** 一度に処理するトークンの数ごとに、初回だけシェーダーの準備が走る(実測。ドライバーが exe ごとに
  覚えるので、同じ exe の 2 回目の起動からは速い)。UI スレッドを止めるので、読み込みの最後に 1〜16・24・32…256 トークンで 1 回ずつ空回しする
  (`WarmUpGpu()`)。これで初めて起動したときの読み込みは約 3 秒、2 回目からは約 0.55 秒(別スレッドなので変換は止まらない)。
- メモリ: VRAM に載せても `Meltype.exe` のワーキングセットは減らず、Vulkan のドライバーの分だけ増える(手元で約 265MB → 427MB)。

## トレイのメニューの表示

#35。右クリックのメニューにバージョンと zenz の状態を出す(表示専用。開くたびに更新)。状態は `LlamaZenzScorer.Describe()`
(有効・未導入・待機中・読み込み中・読み込めませんでした。設定 OFF は `CompositionService.ZenzStatus` が「OFF (設定)」にする)。
展開場所は読み込めたときだけ出し、GPU に載せたなら「VRAM (GPU・Vulkan)」、そうでなければ「メモリ (CPU)」。
Vulkan 版でも GPU が無い・載せられずに CPU で読み込み直したときは後者になる。読み込んだ後に設定を変えても、起動し直すまで表示は変わらない。

## 実測(手元の Windows 11・CPU・6 スレッド、2026-10-18)

- 1 回の採点(候補 5 件)は 20〜70ms。文脈 + 読みが前回と同じ頭なら、その分の計算を省ける。モデルの読み込みは約 130ms。
- `--perf`(Mozc、5 回、cold は Mozc と zenz の控えを捨ててから打つ)。**遅くなるのは打鍵で、Space の変換ではない。**
  ライブ変換が打つたびに採点し、Space を押す頃には採点が済んでいるため。

  | 例文 | 1 キー cold p95 / p99 (ms) | 変換 cold p95 / p99 (ms) | 変換の正解 |
  | --- | --- | --- | --- |
  | `tools/zenz-rerank-cases.tsv`(14 文)導入前 → 後 | 2.41 / 2.98 → 40.4 / 58.6 | 1.04 / 1.62 → 1.03 / 1.59 | 11 → 14 |
  | `tools/mozc-compare-cases.tsv`(60 文)導入前 → 後 | 1.71 / 2.60 → 38.8 / 55.2 | 1.58 / 1.94 → 2.43 / 96.1 | 51 → 51 |

- ワーキングセットはモデルの分だけ増える(約 90MB → 195MB)。
- **GPU(Vulkan 版・RX 9060 XT、2026-10-18、#29)。** 上の「後」を CPU として、同じ条件の `--perf` で比べた。正解の数は変わらない。

  | 例文 | 1 キー cold p95 / p99 (ms) CPU → GPU | 変換 cold p95 / p99 (ms) CPU → GPU |
  | --- | --- | --- |
  | `tools/zenz-rerank-cases.tsv` | 40.4 / 58.6 → 16.9 / 25.2 | 1.03 / 1.59 → 1.07 / 1.45 |
  | `tools/mozc-compare-cases.tsv` | 38.8 / 55.2 → 16.7 / 23.6 | 2.43 / 96.1 → 2.93 / 42.4 |

  採点だけを測ると(候補 5 件・中央値)、毎回違う文脈で 39 → 12〜14ms、文脈が同じで読みが伸びる打鍵で 22 → 5ms、
  3 秒〜120 秒休んだ後の 1 回目も 13〜18ms(GPU の省電力からの戻りで遅くはならなかった)。
- `--perf` は最初に 1 周打ってから測るので、zenz の控えを捨てないと cold でも採点の結果を使い回してしまい、導入後のほうが速く見えた
  (`Performance.cs` の cold の前に `PerformanceZenz.ClearCache()` を足したのはそのため)。

## 落とし穴

- **前の行を文脈に入れると引っぱられる。** 実機(メモ帳)で、前の行に「著名な研究者の講演」があると、街の + こうえん が 公演 になった。
  改行を取り除いて「…研究者の講演街の」として採点していたため(公園 -2.88 / 講演 -0.60 / 公演 -0.94)。そこへ Mozc が前の確認で
  確定した 公演 を先頭に覚えていて、事前の分で 公演 が残った。改行で区切られた行は別の話(メモ帳の行・チャットのメッセージ)のことが
  多いので、最後の改行より後ろだけを使う。句点(。)をまたぐのは使う(上流の文脈が文をまたがないのを補うのが目的のため)。

- **zenz のタグは U+EE00(入力)・U+EE01(出力)・U+EE02(文脈)。** imev の U+E200〜 は誤りで、正解の候補でも対数確率が -7 前後に
  なり、事前に負けて並び替わりにくかった(imev #33 の「C++ の対数確率が PoC より鈍い」の原因)。直すと正解は -0.0〜-0.3 になる。
- `Settings` に項目を足すと設定画面に自動で出る(`SettingsForm` がリフレクションで並べる)。`ZenzRerank` は「1. 全般」に出る。

## 経緯

- #2 で作った。採点の方式は imev(kant0123/imev `02_src/mozc/context-rerank.patch`)から移し、imev で踏んだことは
  [[context-rerank-lessons]] にある。
