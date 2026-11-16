# Bozuk para algoritmasını etiketli veri setinde ölçer ve özeti yazdırır.
# Kullanım: .\coin-eval.ps1 -Dataset <Roboflow veri seti kökü> [-Out <çıktı klasörü>]
param(
    [Parameter(Mandatory = $true)][string]$Dataset,
    [string]$Out = (Join-Path $PSScriptRoot 'sunum\coin_eval')
)
$ErrorActionPreference = 'Stop'
if (-not $env:DOTNET_ROOT -and (Test-Path "$env:LOCALAPPDATA\Microsoft\dotnet")) { $env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet" }
$exe = Get-ChildItem (Join-Path $PSScriptRoot 'bin\Release') -Recurse -Filter GoruntuIslemeOdevleri.exe | Select-Object -First 1
if (-not $exe) { throw 'Önce Release derlemesi yapın: dotnet build -c Release' }
$p = Start-Process $exe.FullName -ArgumentList '--coineval', "`"$Dataset`"", "`"$Out`"" -Wait -PassThru -NoNewWindow
$s = Get-Content (Join-Path $Out 'coin_eval.json') -Raw -Encoding UTF8 | ConvertFrom-Json
"{0} fotoğraf | adet doğru {1} | tür dağılımı doğru {2} | toplam doğru {3} | bulunan para {4}/{5} | ort. hata {6:0.00} TL" -f `
    $s.images, $s.countCorrect, $s.classesCorrect, $s.totalCorrect, $s.coinsFound, $s.coinsTrue, $s.meanAbsTotalError
