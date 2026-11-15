param(
    [Parameter(Mandatory = $false)]
    [string]$DatasetZip,
    [int]$Epochs = 60
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$VenvPython = Join-Path $ProjectRoot '.venv-yolo\Scripts\python.exe'
$Downloads = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads'
$DatasetRoot = Join-Path $ProjectRoot 'data\coins_v2'
$LocalYaml = Join-Path $DatasetRoot 'data.local.yaml'

if (-not $DatasetZip -and -not (Test-Path -LiteralPath $LocalYaml -PathType Leaf)) {
    $DatasetZip = Get-ChildItem -LiteralPath $Downloads -File -Filter '*.zip' |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not (Test-Path -LiteralPath $LocalYaml -PathType Leaf) -and
    (-not $DatasetZip -or -not (Test-Path -LiteralPath $DatasetZip -PathType Leaf))) {
    throw 'Veri ZIP bulunamadi. -DatasetZip ile tam yolu verin.'
}
if (-not (Test-Path -LiteralPath $VenvPython -PathType Leaf)) {
    python -m venv (Join-Path $ProjectRoot '.venv-yolo')
}

& $VenvPython -m pip install --upgrade pip
& $VenvPython -m pip install -r (Join-Path $PSScriptRoot 'requirements.txt')

Push-Location $ProjectRoot
try {
    if (-not (Test-Path -LiteralPath $LocalYaml -PathType Leaf)) {
        & $VenvPython (Join-Path $PSScriptRoot 'prepare_dataset.py') --zip $DatasetZip --output $DatasetRoot --force
    }
    & $VenvPython (Join-Path $PSScriptRoot 'validate_yolo_dataset.py') --root $DatasetRoot --report 'training\dataset_report.json'
    & $VenvPython (Join-Path $PSScriptRoot 'train_coin_detector.py') --data $LocalYaml --epochs $Epochs
}
finally {
    Pop-Location
}

