// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

using System.Runtime.InteropServices;

namespace Meltype.Composition;

/// <summary>
/// MeltKelp: ひらがなを単語 (形態素) に区切る。設定「打ったキーを表示」の「ひらがな (単語ごとに空白)」に使う (MeltKelp #27)。
/// Microsoft IME の形態素解析 (IFELanguage の GetJMorphResult) の単語 (WDD) の読みの位置で区切る
/// (きょうはいいてんきです → きょう は いい てんき です)。上流の <see cref="MsImeKanjiConverter"/> は単語を文節にまとめてしまうので、
/// 上流のファイルに手を入れず、COM の宣言をこのファイルに持つ。
/// COM オブジェクトなので、最初に呼んだスレッド (変換ボックスを動かす UI スレッド) からだけ使う。
/// 使えない・失敗したときは null を返す (呼び出し側は区切らずに出す)。
/// </summary>
public sealed class MsImeWords : IDisposable
{
    private const uint FELANG_REQ_CONV = 0x00010000;
    private const uint FELANG_CMODE_NOINVISIBLECHAR = 0x40000000;

    private IFELanguage? _language;
    private bool _unavailable;

    // 1 キーごとに表示を作り直すので、同じ読みは前の結果を使う
    private string? _lastInput;
    private IReadOnlyList<string>? _lastResult;

    public IReadOnlyList<string>? Split(string hiragana)
    {
        if (string.IsNullOrEmpty(hiragana)) return null;
        if (hiragana == _lastInput) return _lastResult;
        IReadOnlyList<string>? result;
        if (hiragana.Length > MsImeKanjiConverter.MaxInputLength)
        {
            // 長い読みは変換と同じところで区切って解析し、つなげる (区切れなかった部分はそのまま 1 語)
            var words = new List<string>();
            foreach (var part in MsImeKanjiConverter.SplitForEngine(hiragana, MsImeKanjiConverter.MaxInputLength))
                words.AddRange(Morph(part) ?? [part]);
            result = words;
        }
        else result = Morph(hiragana);
        _lastInput = hiragana;
        _lastResult = result;
        return result;
    }

    private List<string>? Morph(string hiragana)
    {
        var language = Open();
        if (language is null) return null;
        var pointer = IntPtr.Zero;
        try
        {
            var hr = language.GetJMorphResult(FELANG_REQ_CONV, FELANG_CMODE_NOINVISIBLECHAR, hiragana.Length, hiragana, IntPtr.Zero, out pointer);
            if (hr < 0 || pointer == IntPtr.Zero) return null;
            return Parse(pointer, hiragana);
        }
        catch (Exception ex)
        {
            // 読みはログに書かない (入力内容は個人情報)
            Diagnostics.Log.Warn($"打ったキーの単語の区切りを調べられませんでした ({hiragana.Length} 文字): {ex.Message}");
            return null;
        }
        finally
        {
            if (pointer != IntPtr.Zero) Marshal.FreeCoTaskMem(pointer);
        }
    }

    /// <summary>単語の読みの先頭位置で区切る。エンジンが読みを書き換えた (読みが入力と違う) ときは null。</summary>
    private static List<string>? Parse(IntPtr pointer, string input)
    {
        var result = Marshal.PtrToStructure<MORRSLT>(pointer);
        // 構造体の解釈を誤っていたときに不正なメモリを読まないよう、値の範囲を確かめてから使う (MsImeKanjiConverter.ParseClauses と同じ)。
        if (result.pWDD == IntPtr.Zero || result.cWDD <= 0 || result.cWDD > input.Length * 2 || result.cchRead > input.Length * 4 ||
            result.dwSize < Marshal.SizeOf<MORRSLT>() || result.dwSize > 1 << 20)
        {
            return null;
        }
        var reading = result.pwchRead != IntPtr.Zero ? Marshal.PtrToStringUni(result.pwchRead, result.cchRead) : input;
        if (reading != input) return null;
        var size = Marshal.SizeOf<WDD>();
        var starts = new SortedSet<int>();
        var covered = 0;
        for (var i = 0; i < result.cWDD; i++)
        {
            var word = Marshal.PtrToStructure<WDD>(result.pWDD + i * size);
            if (word.wReadPos + word.cchRead > input.Length) return null;
            if (word.wReadPos > 0 && word.wReadPos < input.Length) starts.Add(word.wReadPos);
            covered = Math.Max(covered, word.wReadPos + word.cchRead);
        }
        if (covered != input.Length) return null;
        var words = new List<string>();
        var start = 0;
        foreach (var next in starts)
        {
            words.Add(input[start..next]);
            start = next;
        }
        words.Add(input[start..]);
        return words;
    }

    private IFELanguage? Open()
    {
        if (_language is not null) return _language;
        if (_unavailable) return null;
        try
        {
            var type = Type.GetTypeFromProgID("MSIME.Japan", throwOnError: true)!;
            var language = (IFELanguage)Activator.CreateInstance(type)!;
            var hr = language.Open();
            if (hr < 0) throw new COMException("IFELanguage.Open が失敗しました。", hr);
            _language = language;
            return language;
        }
        catch (Exception ex)
        {
            _unavailable = true;
            Diagnostics.Log.Warn($"Microsoft IME の形態素解析 (MSIME.Japan) を使えません。打ったキーのひらがなは単語に区切らずに出します: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_language is null) return;
        try
        {
            _language.Close();
            Marshal.ReleaseComObject(_language);
        }
        catch
        {
        }
        _language = null;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)] // msime.h は 1 バイト境界でパックしている
    private struct MORRSLT
    {
        public uint dwSize;
        public IntPtr pwchOutput;
        public ushort cchOutput;
        public IntPtr pwchRead;
        public ushort cchRead;
        public IntPtr pchInputPos;
        public IntPtr pchOutputIdxWDD;
        public IntPtr pchReadIdxWDD;
        public IntPtr paMonoRubyPos;
        public IntPtr pWDD;
        public int cWDD;
        public IntPtr pPrivate;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)] // msime.h は 1 バイト境界でパックしている
    private struct WDD
    {
        public ushort wDispPos;
        public ushort wReadPos;
        public ushort cchDisp;
        public ushort cchRead;
        public uint WDD_nReserve1;
        public ushort nPos;
        public ushort flags;
        public IntPtr pReserved;
    }

    [ComImport, Guid("019F7152-E6DB-11d0-83C3-00C04FDDB82E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFELanguage
    {
        [PreserveSig] int Open();
        [PreserveSig] int Close();
        [PreserveSig] int GetJMorphResult(uint request, uint conversionMode, int length, [MarshalAs(UnmanagedType.LPWStr)] string input, IntPtr info, out IntPtr result);
        [PreserveSig] int GetConversionModeCaps(out uint caps);
        [PreserveSig] int GetPhonetic([MarshalAs(UnmanagedType.BStr)] string text, int start, int length, [MarshalAs(UnmanagedType.BStr)] out string result);
        [PreserveSig] int GetConversion([MarshalAs(UnmanagedType.BStr)] string text, int start, int length, [MarshalAs(UnmanagedType.BStr)] out string result);
    }
}
