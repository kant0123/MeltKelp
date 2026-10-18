// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

using Meltype.Config;

namespace Meltype.Composition;

/// <summary>
/// MeltKelp: 変換ボックス (Meltype キーボード) と候補の一覧の上 (Meltype IME) に出す、打ったキーの文字列を作る (MeltKelp #27)。
/// ひらがなは変換ボックスの読みと同じく、英語と判定した区間は打ったまま・入力途中の子音は子音のままにする。
/// </summary>
public static class TypedKeysFormatter
{
    /// <summary>
    /// 打ったキーを style の形式にする。何も打っていないか「表示しない」なら null。
    /// words はひらがなを単語に区切る (形態素解析)。null・失敗・区切りが読みと合わないときは区切らずに出す (フェイルオープン)。
    /// </summary>
    public static string? Format(CompositionText text, TypedKeysStyle style, Func<string, IReadOnlyList<string>?>? words = null)
    {
        var raw = text.Raw;
        if (raw.Length == 0) return null;
        return style switch
        {
            TypedKeysStyle.None => null,
            TypedKeysStyle.Hiragana => text.RenderSegments(final: false, convert: null),
            TypedKeysStyle.HiraganaSpaced => Spaced(text, words),
            _ => raw,
        };
    }

    /// <summary>英語の区間・日本語の単語ごとに空白で区切ったひらがな。</summary>
    private static string Spaced(CompositionText text, Func<string, IReadOnlyList<string>?>? words)
    {
        var hiragana = text.RenderSegments(final: false, convert: null);
        // 保護範囲 (URL・パスなど) は打ったまま出しているので区切らない
        if (text.IsProtectedRaw) return hiragana;
        // RenderSegments は区間ごとに 英語 = 打ったまま、日本語 = かな (最後の区間だけ入力途中の子音を後ろに付ける) をつないだもの。
        // 同じ長さずつ切り出して、区間ごとの文字列に戻す。
        var pieces = new List<string>();
        var segments = text.Segments();
        var position = 0;
        for (var i = 0; i < segments.Count && position < hiragana.Length; i++)
        {
            var segment = segments[i];
            var length = i == segments.Count - 1 ? hiragana.Length - position : Math.Min(segment.IsEnglish ? segment.Raw.Length : segment.Kana.Length, hiragana.Length - position);
            var part = hiragana.Substring(position, length);
            position += length;
            if (segment.IsEnglish) pieces.Add(part);
            else pieces.AddRange(SplitWords(part, words));
        }
        if (position < hiragana.Length) pieces.Add(hiragana[position..]);
        return string.Join(" ", pieces.Where(p => p.Length > 0));
    }

    /// <summary>日本語の区間を単語に区切る。入力途中の子音 (きょうはk の k) は解析に渡さず、最後の単語に付ける。</summary>
    internal static IReadOnlyList<string> SplitWords(string kana, Func<string, IReadOnlyList<string>?>? words)
    {
        var end = kana.Length;
        while (end > 0 && char.IsAsciiLetter(kana[end - 1])) end--;
        var body = kana[..end];
        var tail = kana[end..];
        IReadOnlyList<string>? split = null;
        if (body.Length > 0 && words is not null)
        {
            try
            {
                split = words(body);
            }
            catch (Exception ex)
            {
                // 区切れなくても打ったキーは出す (変換を止めない)。読みはログに書かない
                Diagnostics.Log.Warn($"打ったキーを単語に区切れませんでした ({body.Length} 文字): {ex.GetType().Name}");
                split = null;
            }
        }
        if (split is not { Count: > 0 } || split.Any(w => w.Length == 0) || string.Concat(split) != body) split = body.Length > 0 ? [body] : [];
        if (tail.Length == 0) return split;
        if (split.Count == 0) return [tail];
        var list = split.ToList();
        list[^1] += tail;
        return list;
    }
}
