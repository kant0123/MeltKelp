---
type: concept
summary: 英字に直して覚えた短い語(no・go)を、英字の略語の後ろの助詞(APIno)にまで使わない理由と、再現するときの罠(学習は検出器に渡さないと効かない)
updated: 2026-10-31
related: [overview]
---

# 覚えた短い英語を、助詞に使わない

F10 などで英字に直した語は `LanguageMemory`(`%LOCALAPPDATA%\Meltype\languages.json`)に覚えられ、次から自動判定より優先される。
2〜3 文字でローマ字として読める語(no・go・ni)は、これが助詞と取り違えられる。

## 内容

`src/Meltype.Core/Composition/CompositionDetector.cs:678 IsEnglishSpan()` の学習の分岐は、短くてローマ字として読み切れる語を
**英語に覚えていても使わない**場面を持つ。

- 日本語のすぐ後ろ(`nihon|go` を にほんgo にしない。上流の既存の歯止め)
- 入力の途中で、前の区間にくっついているとき(`API|no` を APIno にしない。MeltKelp #49)

単独で打った `no`、英文の続きの `no`(入力の先頭 = `startOfInput`)には、覚えた効果がそのまま残る。

## なぜこうなっているか

#49: 実機で APIno と打つと「APIの」にならず「APIno」のまま確定された。原因は、前に `no` を F10 で英字にして確定した 1 回の学習。
明示(F10)の学習は 1 回で効くので(`src/Meltype.Core/Composition/LanguageMemory.cs:54 NeedsTwice()`)、その後は英語の後ろの `no` がすべて英字になり、
`APInoyouni` も `APInoように` になっていた。英語の語どうしは空白で分かれるので、英字の区間にくっついた 2〜3 文字の語は助詞とみなしてよい。

## 調べるときの罠

- **テストで学習を再現するには `CompositionTests.Detector.Memory` に入れる。** `Keyboard(languages: …)` は変換側の
  `CompositionOptions.Languages` に渡るだけで、判定に使う `CompositionDetector.Memory` は別。実機は
  `CompositionService` のコンストラクターで両方に渡している。`Keyboard` だけに渡すと「学習しても再現しない」という誤った結論になる
  (#49 の調査で踏んだ)。検出器は共有なので、`finally` で元に戻す(`src/Meltype.Core.Tests/ApiParticleTests.cs:13 Typed()`)
- `--repro` は学習・ユーザー辞書・設定の無い状態なので、学習が原因の不具合は再現しない。実機のデータは
  `languages.json` の `English` / `Explicit` / `Count` だけ見れば足りる(入力した語そのものは Issue・PR に書かない)
- 英数(直接入力)状態では、大文字で始まる語や英語と決まった語の後ろは判定をやめて素通しする(`CompositionController.cs` の
  「大文字で始まる語は英語なのでそのまま通す」)。これは仕様で、#49 の原因ではなかった。症状が似ている(APIno のまま)ので、
  先にトレイの「あ」「A」で切り分ける

## 検討して採らなかった選択肢

| 案 | 却下した理由 |
| --- | --- |
| 明示(F10)の学習でも 2 文字の語は 2 回で覚える | 単独の `no` を英字にしたいという 1 回の意思が効かなくなる。問題は覚えること自体でなく、助詞の位置に使うこと |
| 助詞の位置(英語の直後)に限って `no` `ni` だけ除く | 助詞の一覧が二重になる。`go`・`to` など 3 文字以下で読み切れる語は同じ形で起きるので、読み切れる短い語(`Fragment()`)の条件のまま広げた |

## 関連

- [[overview]] — 入口
