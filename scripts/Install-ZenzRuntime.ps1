<#
.SYNOPSIS
  zenz の並べ替え (MeltKelp #2) のランタイムを %LOCALAPPDATA%\Meltype\zenz に置く。

.DESCRIPTION
  置くもの: llama.cpp (azooKey のフォークの b4846、CPU 版) の llama.dll・ggml*.dll と、zenz-v3.1-small のモデル
  (ggml-model-Q5_K_M.gguf)。Meltype.exe (LlamaZenzScorer) は、llama.dll の SHA-256 がこの版のものと一致するときだけ読み込む
  (構造体の形が版ごとに変わり、合わない DLL を呼ぶと Meltype.exe ごと落ちるため)。版を変えるならコードの LlamaSha256 も直す。

  -Source を付けると、そのフォルダーからコピーする (imev の %LOCALAPPDATA%\MozcRerank など、同じ版の一式があるとき)。
  付けなければ GitHub (azooKey/llama.cpp のリリース) と Hugging Face (Miwa-Keita/zenz-v3.1-small-gguf) から取ってくる。
  どちらでも、置いた後に SHA-256 を確かめ、違えば止める。上流の配布物・インストール先 (%LOCALAPPDATA%\Programs\Meltype) には触らない。
  置いた後は Meltype を起動し直すと読み込む (トレイの「ログ / 判定理由」に「zenz: 読み込みました」が出る)。

  このファイルは UTF-8 (BOM 付き) で保存すること (Windows PowerShell 5.1 が日本語を読めずにパースエラーになる)。

.PARAMETER Source
  一式をコピーしてくるフォルダー。

.PARAMETER Destination
  置き場所。既定は %LOCALAPPDATA%\Meltype\zenz。
#>
param(
    [string]$Source,
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Meltype\zenz')
)

$ErrorActionPreference = 'Stop'
$tag = 'b4846'
$expected = @{
    'llama.dll'              = '4c455a39dd04d8fcf0bbd068fa41f45ed0ea02e2260ceeba10acea64615d5e67'
    'ggml.dll'               = '2c7475276b521fbcc1cf7c31019afed31ac58741d1536b3b88096641b0174d3b'
    'ggml-base.dll'          = '45af0697d5d203ab6bb1b43debcd2245164f1c51f48da98735e47bf3698b378b'
    'ggml-cpu.dll'           = '7ecda74eb3b8256d7d3ac21f7be0a2a30549ad9cb098c1715153405ddcd04de8'
    'ggml-rpc.dll'           = '5aac68f6e174a2148ba4a807a898b6615d0af8f483dcfb8c66dae75b0982033f'
    'ggml-model-Q5_K_M.gguf' = '4de930c06bef8c263aa1aa40684af206db4ce1b96375b3b8ed0ea508e0b14f6c'
}

New-Item -ItemType Directory -Force $Destination | Out-Null
if ($Source) {
    foreach ($name in $expected.Keys) { Copy-Item (Join-Path $Source $name) (Join-Path $Destination $name) -Force }
} else {
    $work = Join-Path $env:TEMP "meltkelp-zenz-$tag"
    New-Item -ItemType Directory -Force $work | Out-Null
    $zip = Join-Path $work "llama-$tag.zip"
    Invoke-WebRequest "https://github.com/azooKey/llama.cpp/releases/download/$tag/llama-$tag-bin-win-avx-x64.zip" -OutFile $zip -UseBasicParsing
    $unpacked = Join-Path $work 'bin'
    Expand-Archive $zip -DestinationPath $unpacked -Force
    foreach ($name in 'llama.dll', 'ggml.dll', 'ggml-base.dll', 'ggml-cpu.dll', 'ggml-rpc.dll') {
        Copy-Item (Join-Path $unpacked $name) (Join-Path $Destination $name) -Force
    }
    $model = Join-Path $Destination 'ggml-model-Q5_K_M.gguf'
    if (-not (Test-Path $model)) {
        $part = "$model.part"
        Invoke-WebRequest 'https://huggingface.co/Miwa-Keita/zenz-v3.1-small-gguf/resolve/main/ggml-model-Q5_K_M.gguf' -OutFile $part -UseBasicParsing
        Move-Item $part $model -Force
    }
}

$bad = @()
foreach ($name in $expected.Keys) {
    $hash = (Get-FileHash (Join-Path $Destination $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $expected[$name]) { $bad += $name }
}
if ($bad.Count -gt 0) { throw "SHA-256 が想定と違います: $($bad -join ', ')" }
Write-Host "zenz のランタイムを置きました: $Destination"
