<#
.SYNOPSIS
  zenz の並べ替え (MeltKelp #2) のランタイムを %LOCALAPPDATA%\Meltype\zenz に置く。

.DESCRIPTION
  置くもの: llama.cpp (azooKey のフォークの b4846。既定は CPU 版、-Gpu で Vulkan 版) の llama.dll・ggml*.dll と、
  zenz-v3.1-small のモデル (ggml-model-Q5_K_M.gguf)。Meltype.exe (LlamaZenzScorer) は、llama.dll の SHA-256 がこれらの版のものと
  一致するときだけ読み込む (構造体の形が版ごとに変わり、合わない DLL を呼ぶと Meltype.exe ごと落ちるため)。版を変えるならコードの
  LlamaBuilds も直す。
  DLL は CPU 版を zenz\cpu、Vulkan 版を zenzulkan に置き、モデルは zenz に 1 つ置く。両方置いておけば、どちらを使うかは
  Meltype の設定「zenz を GPU で動かす」で選べる (MeltKelp #29・#31)。両方置くには、-Gpu を付けて 1 回・付けずに 1 回実行する。
  #31 より前の置き方 (zenz の直下の DLL) は消す。

  -Source を付けると、そのフォルダーからコピーする (imev の %LOCALAPPDATA%\MozcRerank など、同じ版の一式があるとき)。
  付けなければ GitHub (azooKey/llama.cpp のリリース) と Hugging Face (Miwa-Keita/zenz-v3.1-small-gguf) から取ってくる。
  どちらでも、置いた後に SHA-256 を確かめ、違えば止める。上流の配布物・インストール先 (%LOCALAPPDATA%\Programs\Meltype) には触らない。
  Meltype が動いていると DLL を掴んでいて上書きできないので、トレイから終了してから実行する。
  置いた後は Meltype を起動し直すと読み込む (トレイの「ログ / 判定理由」に「zenz: 読み込みました (GPU (Vulkan)・…)」などが出る)。

  このファイルは UTF-8 (BOM 付き) で保存すること (Windows PowerShell 5.1 が日本語を読めずにパースエラーになる)。

.PARAMETER Source
  一式をコピーしてくるフォルダー。

.PARAMETER Destination
  置き場所。既定は %LOCALAPPDATA%\Meltype\zenz。

.PARAMETER Gpu
  Vulkan 版 (llama-b4846-bin-win-vulkan-x64.zip) を zenzulkan に置く。付けなければ CPU 版 (llama-b4846-bin-win-avx-x64.zip) を zenz\cpu に置く。
#>
param(
    [string]$Source,
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Meltype\zenz'),
    [switch]$Gpu
)

$ErrorActionPreference = 'Stop'
$tag = 'b4846'
# CPU 版と Vulkan 版で、同じ名前の DLL でも中身が違う (Vulkan 版の ggml.dll は ggml-vulkan.dll に依存する)
if ($Gpu) {
    $build = 'vulkan'
    $expected = @{
        'llama.dll'       = '9463fe42037b98038b298fce936d0e86a827f193eac617c6fff3aa4aad316f11'
        'ggml.dll'        = 'b8bd46f5574c30ea34f2d03f549ae3e2b58ea4b87fa18c1531603bccb09a2df9'
        'ggml-base.dll'   = 'cc48b56abfbca96d47a010def03839191c115dd09419292fb508e8bea6539b2e'
        'ggml-cpu.dll'    = 'ce682c532cbbdb2df3c14277154d1066959bde87fd5547922d69e32a322a6c3c'
        'ggml-rpc.dll'    = 'a058facfcfc134f1d676795e8236b0d1b566b0328bec7911f943cf16d5e6001b'
        'ggml-vulkan.dll' = '4a2e577496b221f87bcd7e7b92f2f887b49f7355472b98c6bd1946a30a52232c'
    }
} else {
    $build = 'avx'
    $expected = @{
        'llama.dll'     = '4c455a39dd04d8fcf0bbd068fa41f45ed0ea02e2260ceeba10acea64615d5e67'
        'ggml.dll'      = '2c7475276b521fbcc1cf7c31019afed31ac58741d1536b3b88096641b0174d3b'
        'ggml-base.dll' = '45af0697d5d203ab6bb1b43debcd2245164f1c51f48da98735e47bf3698b378b'
        'ggml-cpu.dll'  = '7ecda74eb3b8256d7d3ac21f7be0a2a30549ad9cb098c1715153405ddcd04de8'
        'ggml-rpc.dll'  = '5aac68f6e174a2148ba4a807a898b6615d0af8f483dcfb8c66dae75b0982033f'
    }
}
$modelName = 'ggml-model-Q5_K_M.gguf'
$modelHash = '4de930c06bef8c263aa1aa40684af206db4ce1b96375b3b8ed0ea508e0b14f6c'
$folder = if ($Gpu) { 'vulkan' } else { 'cpu' }
$libraryDir = Join-Path $Destination $folder
$model = Join-Path $Destination $modelName

New-Item -ItemType Directory -Force $libraryDir | Out-Null
if ($Source) {
    foreach ($name in $expected.Keys) { Copy-Item (Join-Path $Source $name) (Join-Path $libraryDir $name) -Force }
    Copy-Item (Join-Path $Source $modelName) $model -Force
} else {
    $work = Join-Path $env:TEMP "meltkelp-zenz-$tag-$build"
    New-Item -ItemType Directory -Force $work | Out-Null
    $zip = Join-Path $work "llama-$tag.zip"
    Invoke-WebRequest "https://github.com/azooKey/llama.cpp/releases/download/$tag/llama-$tag-bin-win-$build-x64.zip" -OutFile $zip -UseBasicParsing
    $unpacked = Join-Path $work 'bin'
    Expand-Archive $zip -DestinationPath $unpacked -Force
    foreach ($name in $expected.Keys) {
        Copy-Item (Join-Path $unpacked $name) (Join-Path $libraryDir $name) -Force
    }
    if (-not (Test-Path $model)) {
        $part = "$model.part"
        Invoke-WebRequest 'https://huggingface.co/Miwa-Keita/zenz-v3.1-small-gguf/resolve/main/ggml-model-Q5_K_M.gguf' -OutFile $part -UseBasicParsing
        Move-Item $part $model -Force
    }
}

$bad = @()
foreach ($name in $expected.Keys) {
    $hash = (Get-FileHash (Join-Path $libraryDir $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $expected[$name]) { $bad += "$folder\$name" }
}
if ((Get-FileHash $model -Algorithm SHA256).Hash.ToLowerInvariant() -ne $modelHash) { $bad += $modelName }
if ($bad.Count -gt 0) { throw "SHA-256 が想定と違います: $($bad -join ', ')" }
# #31 より前は zenz の直下に DLL を置いていた。残すと、片方のサブフォルダーしか無い設定のときに直下の版が使われ、どちらが動いているか分かりにくい
foreach ($old in Get-ChildItem $Destination -File -Filter '*.dll') { Remove-Item $old.FullName }
Write-Host "zenz のランタイム ($build) を置きました: $libraryDir"
