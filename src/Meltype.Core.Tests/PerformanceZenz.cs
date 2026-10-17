// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

using Meltype.Composition;

namespace Meltype.Tests;

/// <summary>
/// MeltKelp #2: --perf で、アプリと同じく変換エンジンを zenz の並べ替えで包む (採点の分の遅延を測るため)。
/// ランタイム (%LOCALAPPDATA%\Meltype\zenz) が無いとき・MELTKELP_ZENZ=0 のときは包まない (上流と同じ経路で測る)。
/// </summary>
internal static class PerformanceZenz
{
    private static ZenzRerankingConverter? s_converter;

    /// <summary>cold で打つ前に、変換エンジンの控えと一緒に採点の控えも捨てる。</summary>
    public static void ClearCache() => s_converter?.ClearCache();

    public static IKanjiConverter? Wrap(IKanjiConverter? inner, Func<string, IReadOnlyList<string>>? candidates)
    {
        if (inner is null || Environment.GetEnvironmentVariable("MELTKELP_ZENZ") == "0")
        {
            Console.WriteLine("zenz: 使わない");
            return inner;
        }
        var scorer = new LlamaZenzScorer();
        if (!scorer.WaitUntilReady(60_000))
        {
            Console.WriteLine("zenz: ランタイムが無い・読み込めないので使わない");
            return inner;
        }
        Console.WriteLine("zenz: 使う");
        return s_converter = new ZenzRerankingConverter(inner, candidates ?? (_ => []), scorer, () => true);
    }
}
