---
type: component
summary: 設定「打ったキーを表示」の形式(ローマ字・表示しない・ひらがな・単語ごとに空白を入れたひらがな)。上流の ON / OFF のキーを残したままプルダウンにした仕組みと、単語の区切りに Microsoft IME の形態素解析を使う理由
updated: 2026-10-30
related: [overview]
---

# 打ったキーの表示形式

上流の設定「打ったキーを表示」(ON / OFF、ローマ字だけ)を、形式を選ぶプルダウンにした(#27)。
ローマ字(kyouhagoogle)/ 表示しない / ひらがな(きょうはgoogle)/ ひらがな (単語ごとに空白)(きょう は google)。

## 責務

- 変換ボックス(Meltype キーボード)と候補の一覧の上(Meltype IME)の両方に、同じ文字列(`CompositionView.Typed`)を出す。
  Meltype IME へはパイプの JSON の `typed` の中身が変わるだけで、`native/tip/` とパイプの形式は変えていない
- ひらがなは変換ボックスの読みと同じ作り方(`CompositionText.RenderSegments()`)。英語と判定した区間は打ったまま、
  入力途中の子音(きょうはk の k)は子音のまま、URL などの保護範囲は打ったまま
- 変換中に選び直した候補・区切り直した文節は反映しない。出すのは打ったキーの読み

## 入口となるファイル

| パス | 役割 |
| --- | --- |
| `src/Meltype.Core/Config/Settings.MeltKelp.cs:22 TypedKeysDisplay` | 設定画面のプルダウン。保存はしない(`JsonIgnore`)。上流の `ShowTypedKeys`(ON / OFF)と `TypedKeysFormat`(形式)に読み書きを振り分ける |
| `src/Meltype.Core/Composition/TypedKeysFormatter.cs:18 Format()` | 形式ごとの文字列を作る。単語に区切れないときは区切らずに出す |
| `src/Meltype.Core/Composition/CompositionController.cs:3047 TypedKeys()` | 上流への差し込み口(1 行)。`CompositionOptions.TypedKeysFormat`・`TypedKeysWords` は `CompositionOptions.MeltKelp.cs` に足した |
| `src/Meltype/Composition/MsImeWords.cs:28 Split()` | Microsoft IME の形態素解析で、ひらがなを単語に区切る |
| `src/Meltype/Composition/CompositionService.cs:46 CompositionService()` | コンストラクターで `MsImeWords`(フィールド `_words`)を `TypedKeysWords` に渡す差し込み口 |

## 依存関係

- 依存している: Microsoft IME(`MSIME.Japan` の IFELanguage)。Mozc を変換エンジンに選んでいても、単語の区切りには Microsoft IME を使う
- 依存されている: Meltype キーボードの変換ボックス(`src/Meltype/Composition/CompositionWindow.cs:277 ShowsTyped()`)、
  Meltype IME の候補の一覧(`native/tip/CandidateWindow.cpp` が `typed` を描く)

## 落とし穴

- **ON / OFF は上流の `ShowTypedKeys` のまま持っている。** 「表示しない」は `ShowTypedKeys = false` で、形式(`TypedKeysFormat`)は
  前に選んだものを残す。上流のコード(`TrayApplicationContext` など)は `ShowTypedKeys` を見続けるので、上流の取り込みで
  `ShowTypedKeys` が別の意味に変わったら、`TypedKeysDisplay` の振り分けも見直す。#27 より前の config.json(形式のキーが無い)は、
  ON ならローマ字・OFF なら「表示しない」になる
- 上流の `ShowTypedKeys`(`Settings.cs`)には `[Browsable(false)]` を付けて設定画面から隠し、代わりに partial の `Settings.MeltKelp.cs` に
  `TypedKeysDisplay` を置いた。設定画面は `Settings` のプロパティを宣言順に並べるので、プルダウンは「1. 全般」の中で
  上流の項目より後ろ(zenz の項目の並び)に出る。上流の位置には戻せない
- **Meltype キーボードでは、変換前のひらがなは出ない。** 変換ボックスは打ったキーが見えている文字と同じなら出さない
  (`ShowsTyped()`、上流の仕様)。変換前は変換ボックスの文字がそのひらがななので、変換を始めたときから出る。
  単語ごとに空白を入れた形式は空白の分だけ違うので、変換前も出る
- `MsImeWords` は COM オブジェクトで、最初に呼んだスレッド(変換ボックスを動かす UI スレッド)からだけ使う。
  `MsImeKanjiConverter` と同じ IFELanguage を別のインスタンスで開いている
- 区切りは解析結果の単語(WDD)の読みの先頭位置。エンジンが読みを書き換えた・つなげると元の読みにならないときは区切らない。
  100 文字を超える読みは Microsoft IME が受け付けないので、変換と同じところ(`MsImeKanjiConverter.SplitForEngine()`)で切って解析する

## 経緯

- #27。単語の区切りを Microsoft IME にしたのは、Mozc のヘルパー(`native/mozc/meltype_mozc_helper.cc`)が返すのが
  文節(今日は / いい / 天気です)で、単語(形態素)の区切りを取るにはヘルパーを変えて Bazel で作り直す必要があるため。
  上流の `MsImeKanjiConverter` も同じ `GetJMorphResult` の単語を文節にまとめて使っていて、まとめる前の単語がそのまま形態素になる。
  上流のファイルに手を入れないよう、COM の宣言は `MsImeWords.cs` に複製した
- 英語と判定した区間はローマ字を かな にしない(google を ごおgぇ にしない)。打ったキーのうち日本語として読んだ部分だけをひらがなにする
