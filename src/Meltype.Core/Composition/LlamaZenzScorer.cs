// SPDX-License-Identifier: GPL-3.0-or-later
// MeltKelp が足したファイル (上流の Meltype には無い)。

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Meltype.Composition;

/// <summary>
/// MeltKelp: zenz-v3.1-small を llama.cpp (azooKey の b4846) で動かして、変換候補を採点する (MeltKelp #2)。
/// imev の LlmScorer::Impl::Rank() (kant0123/imev 02_src/mozc/context-rerank.patch) を C# に移したもの。
///   プロンプト: [BOS] U+EE02 文脈 U+EE00 読みのカタカナ U+EE01 → 候補 (azooKey の ZenzPromptBuilder の v3 と同じ)。
///   imev は U+E200〜U+E202 を使っていて (誤り)、対数確率が全体に 7 ほど低く出ていた (imev #33 の「鈍さ」)。文脈 + 読みの KV を系列 0 に置いて使い回し、
///   候補ごとに系列を分けて (seq_cp) 1 回の decode でまとめて採点する。終わったら (途中で失敗しても) 分けた系列を消す。
/// ランタイムは %LOCALAPPDATA%\Meltype\zenz に置く (scripts/Install-ZenzRuntime.ps1)。モデルは zenz に 1 つ、llama.dll・ggml*.dll は
/// CPU 版を zenz\cpu、Vulkan 版を zenzulkan に並べて置き、設定 (ZenzUseGpu) でどちらを読み込むかを選ぶ (MeltKelp #31。LibraryDirectory())。
/// llama.dll の構造体 (llama_model_params など) は版ごとに形が変わり、合わない DLL を呼ぶと Meltype.exe ごと落ちるので、
/// 中身を確かめた b4846 の llama.dll (SHA-256 が一致するもの) しか読み込まない。無い・合わない・読み込めなければ採点しない (フェイルオープン)。
/// Vulkan 版を読み込んで GPU が見つかれば、モデルを全部 GPU に載せる (見つからない・載せられなければ CPU)。
/// 読み込みは別スレッド。採点は UI スレッドから呼ばれ、deadline を過ぎると llama_decode を打ち切る (abort callback。CPU のときだけ効く)。
/// </summary>
public sealed unsafe class LlamaZenzScorer : IConversionScorer
{
    /// <summary>
    /// 読み込んでよい llama.dll (SHA-256 → 配布物)。どちらも azooKey/llama.cpp b4846 で、同じコミットなので構造体の形も同じ。
    /// avx は llama-b4846-bin-win-avx-x64.zip (CPU)、vulkan は llama-b4846-bin-win-vulkan-x64.zip (GPU で採点する。MeltKelp #29)。
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> LlamaBuilds = new Dictionary<string, string>
    {
        ["4c455a39dd04d8fcf0bbd068fa41f45ed0ea02e2260ceeba10acea64615d5e67"] = "avx",
        ["9463fe42037b98038b298fce936d0e86a827f193eac617c6fff3aa4aad316f11"] = "vulkan",
    };
    internal const string ModelFile = "ggml-model-Q5_K_M.gguf";

    private static readonly string InputTag = ((char)0xEE00).ToString();
    private static readonly string OutputTag = ((char)0xEE01).ToString();
    private static readonly string ContextTag = ((char)0xEE02).ToString();
    private const int MaxCandidates = 8;
    private const int Batch = 256;
    private const int ContextSize = 512;

    public static string DefaultDirectory => Path.Combine(Config.AppPaths.DataDirectory, "zenz");

    private readonly string _directory;
    private readonly Func<bool> _useGpu;
    private readonly object _gate = new();
    private int _loading;
    private volatile bool _ready;

    // llama.dll の関数
    private delegate* unmanaged[Cdecl]<LlamaModelParams> _modelDefaultParams;
    private delegate* unmanaged[Cdecl]<LlamaContextParams> _contextDefaultParams;
    private delegate* unmanaged[Cdecl]<byte*, LlamaModelParams, IntPtr> _modelLoad;
    private delegate* unmanaged[Cdecl]<IntPtr, LlamaContextParams, IntPtr> _initFromModel;
    private delegate* unmanaged[Cdecl]<IntPtr, IntPtr> _getVocab;
    private delegate* unmanaged[Cdecl]<IntPtr, int> _nTokens;
    private delegate* unmanaged[Cdecl]<IntPtr, byte*, int, int*, int, byte, byte, int> _tokenize;
    private delegate* unmanaged[Cdecl]<int, int, int, LlamaBatch> _batchInit;
    private delegate* unmanaged[Cdecl]<IntPtr, LlamaBatch, int> _decode;
    private delegate* unmanaged[Cdecl]<IntPtr, int, float*> _getLogits;
    private delegate* unmanaged[Cdecl]<IntPtr, int, int, int, byte> _seqRm;
    private delegate* unmanaged[Cdecl]<IntPtr, int, int, int, int, void> _seqCp;
    private delegate* unmanaged[Cdecl]<IntPtr, void> _kvClear;
    private delegate* unmanaged[Cdecl]<IntPtr, delegate* unmanaged[Cdecl]<IntPtr, byte>, IntPtr, void> _setAbort;
    private delegate* unmanaged[Cdecl]<IntPtr, int> _vocabBos;
    private delegate* unmanaged[Cdecl]<IntPtr, byte> _vocabAddBos;

    private IntPtr _model;
    private IntPtr _context;
    private IntPtr _vocab;
    private LlamaBatch _batch;
    private int _vocabSize;
    private int[] _bos = [];

    // 系列 0 に置いている文脈 + 読みのトークンと、その最後の位置の logits
    private int[] _prefix = [];
    private float[] _prefixLogits = [];

    // abort callback が見る締め切り (Environment.TickCount64)。採点は 1 つずつ (_gate) なので static で足りる
    private static long s_deadline;

    /// <param name="useGpu">Vulkan 版を使うか (設定の ZenzUseGpu)。読み込むときに 1 回だけ見る。</param>
    public LlamaZenzScorer(string? directory = null, Func<bool>? useGpu = null)
    {
        _directory = directory ?? DefaultDirectory;
        _useGpu = useGpu ?? (() => true);
    }

    public bool IsReady => _ready;

    /// <summary>ランタイムの一式が置いてあるか。</summary>
    public bool IsInstalled => File.Exists(Path.Combine(_directory, ModelFile)) && LibraryDirectory(_directory, _useGpu()) is not null;

    /// <summary>
    /// llama.dll・ggml*.dll を読み込むフォルダー。GPU なら vulkan → cpu、CPU なら cpu の順に、llama.dll があるものを選ぶ。
    /// どちらも無ければ zenz の直下 (#31 より前の置き方)。直下が Vulkan 版でも、CPU の設定なら Load() が読み込まない。
    /// 同じ名前の DLL を 1 つのプロセスで読み替えられない (llama.dll の依存が先に読み込んだ ggml.dll に結び付く) ので、
    /// 設定を変えても、起動し直すまで最初に選んだものを使う。
    /// </summary>
    internal static string? LibraryDirectory(string root, bool useGpu)
    {
        string[] candidates = useGpu ? ["vulkan", "cpu", ""] : ["cpu", ""];
        foreach (var name in candidates)
        {
            var dir = name.Length == 0 ? root : Path.Combine(root, name);
            if (File.Exists(Path.Combine(dir, "llama.dll"))) return dir;
        }
        return null;
    }

    public void EnsureLoading()
    {
        if (Interlocked.Exchange(ref _loading, 1) != 0) return;
        if (!IsInstalled)
        {
            Diagnostics.Log.Info(_useGpu()
                ? "zenz: ランタイムが無いので、候補の並べ替えはしません (%LOCALAPPDATA%\\Meltype\\zenz)"
                : "zenz: GPU を使わない設定で CPU 版のランタイムが無いので、候補の並べ替えはしません (%LOCALAPPDATA%\\Meltype\\zenz\\cpu)");
            return;
        }
        new Thread(() =>
        {
            try
            {
                lock (_gate) Load();
            }
            catch (Exception ex)
            {
                Diagnostics.Log.Warn($"zenz: 読み込みに失敗しました: {ex.GetType().Name}");
            }
        }) { IsBackground = true, Name = "zenz loader", Priority = ThreadPriority.BelowNormal }.Start();
    }

    /// <summary>読み込みを待つ (テスト・計測用)。</summary>
    public bool WaitUntilReady(int timeoutMs)
    {
        EnsureLoading();
        if (!IsInstalled) return false;
        var until = Environment.TickCount64 + timeoutMs;
        while (!_loadFinished && Environment.TickCount64 < until) Thread.Sleep(20);
        return _ready;
    }

    private volatile bool _loadFinished;

    private void Load()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var useGpu = _useGpu();
            if (LibraryDirectory(_directory, useGpu) is not { } libraries) return;
            var llama = Path.Combine(libraries, "llama.dll");
            string? build;
            using (var stream = File.OpenRead(llama))
            {
                if (!LlamaBuilds.TryGetValue(Convert.ToHexStringLower(SHA256.HashData(stream)), out build))
                {
                    Diagnostics.Log.Warn("zenz: llama.dll が想定の版 (azooKey/llama.cpp b4846) ではないので読み込みません");
                    return;
                }
            }
            if (build == "vulkan" && !useGpu)
            {
                Diagnostics.Log.Warn("zenz: GPU を使わない設定ですが、CPU 版のランタイム (zenz\\cpu) が無いので読み込みません");
                return;
            }
            // llama.dll が依存する ggml*.dll を先に同じ場所から読み込んでおく (既定の検索順で別の場所の DLL を拾わない)。
            // Vulkan 版の ggml.dll は ggml-vulkan.dll に依存する (CPU 版には無い)
            var backends = build == "vulkan" ? new[] { "ggml-base.dll", "ggml-cpu.dll", "ggml-vulkan.dll", "ggml-rpc.dll", "ggml.dll" } : ["ggml-base.dll", "ggml.dll", "ggml-cpu.dll", "ggml-rpc.dll"];
            foreach (var name in backends)
            {
                var path = Path.Combine(libraries, name);
                if (File.Exists(path)) NativeLibrary.Load(path);
            }
            var handle = NativeLibrary.Load(llama);
            var ggml = NativeLibrary.Load(Path.Combine(libraries, "ggml.dll"));
            if (NativeLibrary.TryGetExport(ggml, "ggml_backend_load_all_from_path", out var loadAll))
            {
                var dir = Utf8(libraries);
                fixed (byte* p = dir) ((delegate* unmanaged[Cdecl]<byte*, void>)loadAll)(p);
            }
            _modelDefaultParams = (delegate* unmanaged[Cdecl]<LlamaModelParams>)Export(handle, "llama_model_default_params");
            _contextDefaultParams = (delegate* unmanaged[Cdecl]<LlamaContextParams>)Export(handle, "llama_context_default_params");
            _modelLoad = (delegate* unmanaged[Cdecl]<byte*, LlamaModelParams, IntPtr>)Export(handle, "llama_model_load_from_file");
            _initFromModel = (delegate* unmanaged[Cdecl]<IntPtr, LlamaContextParams, IntPtr>)Export(handle, "llama_init_from_model");
            _getVocab = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)Export(handle, "llama_model_get_vocab");
            _nTokens = (delegate* unmanaged[Cdecl]<IntPtr, int>)Export(handle, "llama_vocab_n_tokens");
            _tokenize = (delegate* unmanaged[Cdecl]<IntPtr, byte*, int, int*, int, byte, byte, int>)Export(handle, "llama_tokenize");
            _batchInit = (delegate* unmanaged[Cdecl]<int, int, int, LlamaBatch>)Export(handle, "llama_batch_init");
            _decode = (delegate* unmanaged[Cdecl]<IntPtr, LlamaBatch, int>)Export(handle, "llama_decode");
            _getLogits = (delegate* unmanaged[Cdecl]<IntPtr, int, float*>)Export(handle, "llama_get_logits_ith");
            _seqRm = (delegate* unmanaged[Cdecl]<IntPtr, int, int, int, byte>)Export(handle, "llama_kv_cache_seq_rm");
            _seqCp = (delegate* unmanaged[Cdecl]<IntPtr, int, int, int, int, void>)Export(handle, "llama_kv_cache_seq_cp");
            _kvClear = (delegate* unmanaged[Cdecl]<IntPtr, void>)Export(handle, "llama_kv_cache_clear");
            _setAbort = (delegate* unmanaged[Cdecl]<IntPtr, delegate* unmanaged[Cdecl]<IntPtr, byte>, IntPtr, void>)Export(handle, "llama_set_abort_callback");
            _vocabBos = (delegate* unmanaged[Cdecl]<IntPtr, int>)Export(handle, "llama_vocab_bos");
            _vocabAddBos = (delegate* unmanaged[Cdecl]<IntPtr, byte>)Export(handle, "llama_vocab_get_add_bos");
            ((delegate* unmanaged[Cdecl]<void>)Export(handle, "llama_backend_init"))();

            var modelParams = _modelDefaultParams();
            var gpu = build == "vulkan" && HasGpu(ggml, NativeLibrary.Load(Path.Combine(libraries, "ggml-base.dll")));
            modelParams.NGpuLayers = gpu ? 999 : 0;
            modelParams.UseMmap = 1;
            var modelPath = Utf8(Path.Combine(_directory, ModelFile));
            fixed (byte* p = modelPath) _model = _modelLoad(p, modelParams);
            if (_model == IntPtr.Zero && gpu)
            {
                // VRAM が足りないなどで GPU に載せられなければ CPU で読み込み直す
                Diagnostics.Log.Warn("zenz: モデルを GPU に載せられなかったので CPU で読み込みます");
                gpu = false;
                modelParams.NGpuLayers = 0;
                fixed (byte* p = modelPath) _model = _modelLoad(p, modelParams);
            }
            if (_model == IntPtr.Zero)
            {
                Diagnostics.Log.Warn("zenz: モデルを読み込めませんでした");
                return;
            }
            var contextParams = _contextDefaultParams();
            contextParams.NCtx = ContextSize;
            contextParams.NBatch = Batch;
            contextParams.NSeqMax = MaxCandidates + 1;
            contextParams.NThreads = contextParams.NThreadsBatch = Math.Clamp(Environment.ProcessorCount / 2, 2, 6);
            _context = _initFromModel(_model, contextParams);
            if (_context == IntPtr.Zero)
            {
                Diagnostics.Log.Warn("zenz: コンテキストを作れませんでした");
                return;
            }
            _vocab = _getVocab(_model);
            _vocabSize = _nTokens(_vocab);
            _batch = _batchInit(Batch, 0, 1);
            _bos = _vocabAddBos(_vocab) != 0 ? [_vocabBos(_vocab)] : [];
            _setAbort(_context, &Abort, IntPtr.Zero);
            if (gpu) WarmUpGpu();
            // 最初の decode は計算用のバッファを確保するので遅い。UI スレッドで採点する前に 1 回済ませておく
            var warm = Rank("テスト", "てすと", ["テスト", "手須藤"], Environment.TickCount64 + 10_000);
            _ready = true;
            Diagnostics.Log.Info($"zenz: 読み込みました ({(gpu ? "GPU (Vulkan)" : $"CPU・スレッド {contextParams.NThreads}")}・語彙 {_vocabSize}・{watch.ElapsedMilliseconds} ms・試しの採点 {(warm is null ? "失敗" : "成功")})");
        }
        finally
        {
            _loadFinished = true;
        }
    }

    /// <summary>
    /// GPU では、一度に処理するトークンの数ごとに、初めての形のときだけシェーダーの準備で 200ms 前後かかる (実測。ドライバーが
    /// 覚えるので次に起動したときは速い)。GPU では abort callback が効かず UI スレッドを止めてしまうので、出うる数を先に通しておく。
    /// </summary>
    private void WarmUpGpu()
    {
        if (Tokenize("テスト") is not { Length: > 0 } t) return;
        Volatile.Write(ref s_deadline, Environment.TickCount64 + 30_000);
        var sizes = Enumerable.Range(1, 16).Concat([24, 32, 48, 64, 96, 128, 192, Batch]);
        foreach (var n in sizes)
        {
            var items = Enumerable.Range(0, n).Select(i => (t[0], i, 0)).ToList();
            DecodeItems(items, 0, n);
            _kvClear(_context);
        }
        Reset();
    }

    /// <summary>ggml のバックエンドに GPU の装置があるか (Vulkan 版でも、ドライバーが Vulkan に対応していなければ無い)。</summary>
    private static bool HasGpu(params IntPtr[] libraries)
    {
        // 装置の一覧は ggml.dll、装置の種類は ggml-base.dll にある
        IntPtr Find(string name)
        {
            foreach (var library in libraries)
            {
                if (NativeLibrary.TryGetExport(library, name, out var address)) return address;
            }
            return IntPtr.Zero;
        }
        var count = Find("ggml_backend_dev_count");
        var get = Find("ggml_backend_dev_get");
        var type = Find("ggml_backend_dev_type");
        if (count == IntPtr.Zero || get == IntPtr.Zero || type == IntPtr.Zero) return false;
        var n = ((delegate* unmanaged[Cdecl]<nuint>)count)();
        for (nuint i = 0; i < n; i++)
        {
            // enum ggml_backend_dev_type: 0 = CPU・1 = GPU・2 = ACCEL
            if (((delegate* unmanaged[Cdecl]<IntPtr, int>)type)(((delegate* unmanaged[Cdecl]<nuint, IntPtr>)get)(i)) == 1) return true;
        }
        return false;
    }

    private static IntPtr Export(IntPtr handle, string name) =>
        NativeLibrary.TryGetExport(handle, name, out var address) ? address : throw new EntryPointNotFoundException(name);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte Abort(IntPtr data) => Environment.TickCount64 > Volatile.Read(ref s_deadline) ? (byte)1 : (byte)0;

    public IReadOnlyList<double>? Score(string context, string reading, IReadOnlyList<string> candidates, long deadline)
    {
        if (!_ready || !Monitor.TryEnter(_gate)) return null;
        try
        {
            return Rank(context, reading, candidates, deadline);
        }
        finally
        {
            Monitor.Exit(_gate);
        }
    }

    private double[]? Rank(string context, string reading, IReadOnlyList<string> candidates, long deadline)
    {
        if (candidates.Count == 0 || candidates.Count > MaxCandidates) return null;
        Volatile.Write(ref s_deadline, deadline);

        var target = new List<int>(_bos);
        if (context.Length > 0)
        {
            if (Tokenize(ContextTag + context) is not { } head) return null;
            target.AddRange(head);
        }
        if (Tokenize(InputTag + CompositionText.ToKatakana(reading) + OutputTag) is not { } tail) return null;
        target.AddRange(tail);
        if (!Sync(target)) return null;

        var basePosition = _prefix.Length;
        // 系列を分ける前に全部トークン化しておく (途中で失敗しても、分けかけの系列を KV に残さない)
        var tokens = new List<int[]>();
        foreach (var candidate in candidates)
        {
            if (Tokenize(candidate) is not { Length: > 0 } t) return null;
            tokens.Add(t);
        }
        var items = new List<(int Token, int Position, int Sequence)>();
        var firstRow = new int[candidates.Count];
        for (var i = 0; i < candidates.Count; i++)
        {
            firstRow[i] = items.Count;
            for (var k = 0; k + 1 < tokens[i].Length; k++) items.Add((tokens[i][k], basePosition + k, i + 1));
        }
        if (items.Count > Batch) return null;
        try
        {
            for (var i = 0; i < candidates.Count; i++) _seqCp(_context, 0, i + 1, -1, -1);
            if (items.Count > 0 && !DecodeItems(items, 0, items.Count))
            {
                Reset();
                return null;
            }
            var scores = new double[candidates.Count];
            for (var i = 0; i < candidates.Count; i++)
            {
                var lp = LogProb(_prefixLogits, tokens[i][0]);
                for (var k = 1; k < tokens[i].Length; k++)
                {
                    var row = _getLogits(_context, firstRow[i] + k - 1);
                    if (row == null)
                    {
                        Reset();
                        return null;
                    }
                    lp += LogProb(new ReadOnlySpan<float>(row, _vocabSize), tokens[i][k]);
                }
                scores[i] = lp;
            }
            return scores;
        }
        finally
        {
            for (var i = 0; i < candidates.Count; i++) _seqRm(_context, i + 1, -1, -1);
        }
    }

    /// <summary>系列 0 を target (文脈 + 読み) にする。前回と共通の頭は計算し直さない。失敗したら KV を空にして false。</summary>
    private bool Sync(List<int> target)
    {
        if (target.Count == 0 || target.Count >= ContextSize - 64) return false;
        var common = 0;
        while (common < Math.Min(target.Count, _prefix.Length) && target[common] == _prefix[common]) common++;
        if (common == target.Count && common == _prefix.Length && _prefixLogits.Length > 0) return true;
        common = Math.Min(common, target.Count - 1); // 最後のトークンの logits が要る
        if (common < _prefix.Length && _seqRm(_context, 0, common, -1) == 0)
        {
            Reset();
            common = 0;
        }
        var items = new List<(int Token, int Position, int Sequence)>();
        for (var i = common; i < target.Count; i++) items.Add((target[i], i, 0));
        var last = 0;
        for (var start = 0; start < items.Count; start += Batch)
        {
            var end = Math.Min(items.Count, start + Batch);
            if (!DecodeItems(items, start, end))
            {
                Reset();
                return false;
            }
            last = end - start;
        }
        var row = _getLogits(_context, last - 1);
        if (row == null)
        {
            Reset();
            return false;
        }
        _prefixLogits = new ReadOnlySpan<float>(row, _vocabSize).ToArray();
        _prefix = [.. target];
        return true;
    }

    private bool DecodeItems(List<(int Token, int Position, int Sequence)> items, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            var k = i - start;
            _batch.Token[k] = items[i].Token;
            _batch.Pos[k] = items[i].Position;
            _batch.NSeqId[k] = 1;
            _batch.SeqId[k][0] = items[i].Sequence;
            _batch.Logits[k] = 1;
        }
        _batch.NTokens = end - start;
        return _decode(_context, _batch) == 0;
    }

    private void Reset()
    {
        _kvClear(_context);
        _prefix = [];
        _prefixLogits = [];
    }

    private double LogProb(ReadOnlySpan<float> logits, int token)
    {
        var max = float.NegativeInfinity;
        foreach (var x in logits) if (x > max) max = x;
        double sum = 0;
        foreach (var x in logits) sum += Math.Exp(x - max);
        return logits[token] - max - Math.Log(sum);
    }

    /// <summary>zenz に渡す形にしてトークン化する (改行を除き、半角の空白は全角にする: imev の NormalizeForZenz)。失敗したら null。</summary>
    private int[]? Tokenize(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text.Replace("\r", "").Replace("\n", "").Replace(' ', '　'));
        var tokens = new int[bytes.Length + 8];
        int n;
        fixed (byte* p = bytes)
        fixed (int* t = tokens)
            n = _tokenize(_vocab, p, bytes.Length, t, tokens.Length, 0, 1);
        return n < 0 ? null : tokens[..n];
    }

    private static byte[] Utf8(string text) => [.. Encoding.UTF8.GetBytes(text), 0];

    // ---- llama.h (b4846) の構造体。並び・型はヘッダのとおり (bool は 1 バイト) ----

    [StructLayout(LayoutKind.Sequential)]
    private struct LlamaModelParams
    {
        public IntPtr Devices;
        public int NGpuLayers;
        public int SplitMode;
        public int MainGpu;
        public IntPtr TensorSplit;
        public IntPtr ProgressCallback;
        public IntPtr ProgressCallbackUserData;
        public IntPtr KvOverrides;
        public byte VocabOnly;
        public byte UseMmap;
        public byte UseMlock;
        public byte CheckTensors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LlamaContextParams
    {
        public uint NCtx;
        public uint NBatch;
        public uint NUbatch;
        public uint NSeqMax;
        public int NThreads;
        public int NThreadsBatch;
        public int RopeScalingType;
        public int PoolingType;
        public int AttentionType;
        public float RopeFreqBase;
        public float RopeFreqScale;
        public float YarnExtFactor;
        public float YarnAttnFactor;
        public float YarnBetaFast;
        public float YarnBetaSlow;
        public uint YarnOrigCtx;
        public float DefragThold;
        public IntPtr CbEval;
        public IntPtr CbEvalUserData;
        public int TypeK;
        public int TypeV;
        public byte LogitsAll;
        public byte Embeddings;
        public byte OffloadKqv;
        public byte FlashAttn;
        public byte NoPerf;
        public IntPtr AbortCallback;
        public IntPtr AbortCallbackData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LlamaBatch
    {
        public int NTokens;
        public int* Token;
        public float* Embd;
        public int* Pos;
        public int* NSeqId;
        public int** SeqId;
        public sbyte* Logits;
    }
}
