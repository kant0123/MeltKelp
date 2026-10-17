---
type: concept
summary: zenz の並べ替えを載せる土台に Meltype を選んだ理由。imev での Mozc 本家・Ohagey(azooKey + Zenzai)の評価と、Meltype に移した決め手
updated: 2026-10-17
related: [overview, context-rerank-lessons, build-install]
---

# 土台の選択

zenz で変換候補を文脈つきに並べ替える、という目的は imev から変わっていない。変わったのは載せる土台で、
imev は Mozc 本家(TSF)に Rewriter として載せ、MeltKelp は Meltype の `Meltype.exe` に載せる。

## imev で比べたもの

出典: imev の `wiki/concepts/engine-base-choice.md`(2026-10-04 の評価。i5-12600K、CPU)。数値は imev の評価 11 問のもの。

- **Ohagey(C0uki/Ohagey。azooKey の KanaKanjiConverter + Zenzai を Windows の TSF に載せた IME)は採らなかった。**
  - 精度は imev の PoC と同等(文脈あり 10/11、なし 4/11、外したのも同じ 1 問)。同じ zenz-v3.1 系なので上積みが無い
  - 遅い。1 回の変換が文脈ありで約 234ms(候補の生成から LM で行うため)。採点だけの方式は 13〜50ms
  - x64 専用で、32 ビットアプリでは TSF が載らない
  - 導入物が未成熟で、インストーラーにリソースが欠けてエンジンが落ちた。Swift のビルド環境も重い
- **Mozc 本家を選んだ。** 辞書・学習・操作系が成熟していて、精度の足りない分だけを LM で補える。32 / 64 ビットの
  TSF があり、Rewriter の口から文脈(カーソルの左の文)が取れるので、本体への変更が小さく済んだ。

ここから引き継ぐ判断: **候補は既存のエンジン(Mozc など)に出させ、LM は並べ替えだけに使う。** 候補の生成まで LM で
行う方式(azooKey の Zenzai)は、精度が同じで遅い。

## なぜ Meltype に移したか

- **英語と日本語の自動判定がある。** 半角/全角を押さずに打ち分けられるのは Meltype の強みで、imev(Mozc 本家)には無い。
- **候補の一覧を Meltype 自身が持っている。** Meltype IME の候補ウィンドウの中身も変換ボックスも `Meltype.exe` が決めるので、
  並べ替えを `Meltype.exe`(C#)の中で完結でき、TSF の DLL にも Mozc 本体にも手を入れずに済む。差し込む場所は
  `HybridConverter`(Mozc と Microsoft IME の組み合わせ)の外側(#2)。
- **上流は文脈を短くしか使っていない。** 変換エンジン(Mozc ヘルパー・Microsoft IME)に渡す文脈は、キャレットの前の
  同じ文の日本語の部分・最大 10 文字だけ(`CompositionController.ConversionContext()`)。文をまたぐ文脈や、
  文脈が取れないアプリでの直前の確定文字列は使われない。並べ替えで取り返せる余地がある。
- **上流は LM の常時実行を入れない。** 上流 #270 は not_planned。上流に入れてもらう道が無いので、フォークで独自に持つ
  ([[overview]] の「主要な設計判断」)。

## 土台を替えても変わらないこと

- **Chrome で自前ビルドの IME が動かない件は直らない。** 原因は ESET が署名の無い TIP DLL を Chrome に読み込ませないことで、
  IME の種類によらない([[build-install]])。MeltKelp では Meltype IME の DLL とパイプの形式を上流と同じに保ち、上流が将来
  署名した DLL を使える可能性を残している。
- Meltype キーボード(変換ボックスで入力し、確定した文字列を送る方式)は TSF の DLL を使わないので、この件の影響を受けない
  と見ている(Chrome での確認はまだしていない)。

## 却下した案

- **imev(Mozc 本家)を続ける。** 英日の自動判定が無く、足すなら Mozc 本体に大きく手を入れることになる。
- **上流の Meltype に並べ替えを入れてもらう。** 上流の方針(#270)と合わない。
- **公式の Meltype と並べて入れられる別の IME にする。** 識別子を全部変えることになり、上流の取り込みと署名済み DLL の
  利用の両方を捨てる([[overview]])。
