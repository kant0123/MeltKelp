---
type: component
summary: zenz で漢字変換の最初の候補を文脈つきに選び直す部品(HybridConverter のデコレーターと llama.cpp の採点器)。差し込み口・文脈の作り方・学習との順序・かなの扱い・ランタイムの置き場所と版の固定
updated: 2026-10-18
related: [context-rerank-lessons, engine-base-choice, build-install, overview]
---

# zenz の並べ替え

変換エンジン(`HybridConverter`。Mozc と Microsoft IME)が出した各文節の最初の候補を、キャレットの前の文字と同じ変換の前の文節を
文脈にして、zenz-v3.1-small で選び直す(#2)。候補を足したり消したりはせず、文節の区切りも変えない。`native/tip/` とパイプの
形式には触れていない(`Meltype.exe` の中で完結する)。

## 入口となるファイル

| パス | 役割 |
| --- | --- |
| `src/Meltype.Core/Composition/ZenzRerank.cs:68 ZenzRerankingConverter` | デコレーター本体。`Choose()` が 1 文節分の選び直し、`Candidates()` が ↑↓ の一覧の並べ替え |
| `src/Meltype.Core/Composition/LlamaZenzScorer.cs:214 Rank()` | 採点。imev の `LlmScorer::Impl::Rank()` を C# に移したもの。`Load()` が版の確認と読み込み |
| `src/Meltype.Core/Composition/CompositionController.cs:1393 ConvertJapanese()` | 差し込み口: 変換の直前に `_precedingText` を渡す 1 行 |
| `src/Meltype/Composition/CompositionService.cs:84 CompositionService()` | 差し込み口: `HybridConverter` を包む |
| `scripts/Install-ZenzRuntime.ps1` | ランタイムを `%LOCALAPPDATA%\Meltype\zenz` に置く |

上流のファイルへの変更は、ほかに `Settings`・`CompositionOptions` の `partial`(設定の `ZenzRerank` を別ファイルで足すため)、
`TrayApplicationContext` の 1 行、`Meltype.Core.csproj` の `AllowUnsafeBlocks`、`--perf` の `Performance.cs` の 2 行だけ。

## 仕組みの要点

- **選び直すのは `ConvertClauses()` の各文節の `Text`。** 最初の Space で出る候補はこれで決まり、`Candidates()`(`MoreCandidates`)は
  `CompositionController.Expand()` が先頭を固定したまま後ろに足すだけなので、`Candidates()` だけ並べ替えても先頭は変わらない。
  `Candidates()` は、同じ読みを直前に採点した順に、採点した候補どうしの位置の中で並べ替えるだけ(採点し直さない)。
- **文脈は zenz 用に別に作る。** 上流の文脈(`ConversionContext()`: 同じ文の日本語・最大 10 文字)は使わず、`_precedingText`
  (キャレットの前の確定済みの文字。UI Automation と Meltype IME はどちらも 20 文字までしか読まない)に、同じ変換の前の文節
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
- **時間:** 1 回の `ConvertClauses()` で採点に使ってよいのは合計 150ms(`ZenzRerankOptions.BudgetMs`)。過ぎたら残りの文節は
  変換エンジンの順のまま、締め切りは `llama_set_abort_callback` で decode の途中でも打ち切る。モデルの読み込みは別スレッドで、
  終わるまでは採点しない。読み込みの最後に 1 回試しに採点しておく(最初の decode は計算用のバッファを確保するので遅い)。
- **キャッシュ:** (文脈, 読み, 候補) → 対数確率。**失敗・時間切れも覚える**(ライブ変換は打つたびに変換するので、
  同じ入力で毎回締め切りまで待たないため)。512 件で捨てて覚え直す。

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

- 置き場所は `%LOCALAPPDATA%\Meltype\zenz`(llama.dll・ggml*.dll・`ggml-model-Q5_K_M.gguf`)。上流の配布物とインストール先は変えない。
  データフォルダーのバックアップ(`Config/Backup.cs`)は決まったファイルしか入れないので、74MB のモデルは入らない。
- **llama.dll は azooKey/llama.cpp の b4846(`llama-b4846-bin-win-avx-x64.zip`)のものしか読み込まない**(SHA-256 で確かめる。
  `LlamaZenzScorer.LlamaSha256`)。`llama_model_params` などの構造体を値で受け渡すので、版が違うと形が合わずに `Meltype.exe` ごと
  落ちうる。合わなければ読み込まずに並べ替えをしない(フェイルオープン)。版を上げるなら、ヘッダの構造体を突き合わせてから
  コードとスクリプトのハッシュを両方直す。b4846 の API は `llama_kv_cache_*`(新しい版では `llama_memory_*` に変わっている)。
- 依存する ggml*.dll は、既定の検索順で別の場所の DLL を拾わないよう、同じフォルダーから先に読み込む。ggml-rpc.dll が欠けると
  llama.dll が読み込めない(imev で踏んだ)。
- llama.cpp は読み込みの様子を標準エラーに書く(モデルの情報だけで、入力は書かない)。`Meltype.exe` では捨てられる。

## 実測(手元の Windows 11・CPU・6 スレッド、2026-10-18)

- 1 回の採点(候補 5 件)は 20〜70ms。文脈 + 読みが前回と同じ頭なら、その分の計算を省ける。モデルの読み込みは約 130ms。
- `--perf`(Mozc、5 回、cold は Mozc と zenz の控えを捨ててから打つ)。**遅くなるのは打鍵で、Space の変換ではない。**
  ライブ変換が打つたびに採点し、Space を押す頃には採点が済んでいるため。

  | 例文 | 1 キー cold p95 / p99 (ms) | 変換 cold p95 / p99 (ms) | 変換の正解 |
  | --- | --- | --- | --- |
  | `tools/zenz-rerank-cases.tsv`(14 文)導入前 → 後 | 2.41 / 2.98 → 46.2 / 66.5 | 1.04 / 1.62 → 1.06 / 1.61 | 11 → 14 |
  | `tools/mozc-compare-cases.tsv`(60 文)導入前 → 後 | 1.71 / 2.60 → 46.4 / 66.2 | 1.58 / 1.94 → 2.64 / 113.0 | 51 → 51 |

- ワーキングセットはモデルの分だけ増える(約 90MB → 195MB)。
- `--perf` は最初に 1 周打ってから測るので、zenz の控えを捨てないと cold でも採点の結果を使い回してしまい、導入後のほうが速く見えた
  (`Performance.cs` の cold の前に `PerformanceZenz.ClearCache()` を足したのはそのため)。

## 落とし穴

- **zenz のタグは U+EE00(入力)・U+EE01(出力)・U+EE02(文脈)。** imev の U+E200〜 は誤りで、正解の候補でも対数確率が -7 前後に
  なり、事前に負けて並び替わりにくかった(imev #33 の「C++ の対数確率が PoC より鈍い」の原因)。直すと正解は -0.0〜-0.3 になる。
- `Settings` に項目を足すと設定画面に自動で出る(`SettingsForm` がリフレクションで並べる)。`ZenzRerank` は「1. 全般」に出る。

## 経緯

- #2 で作った。採点の方式は imev(kant0123/imev `02_src/mozc/context-rerank.patch`)から移し、imev で踏んだことは
  [[context-rerank-lessons]] にある。
