$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK bulunamadı. https://dotnet.microsoft.com/download/dotnet/8.0 adresinden x64 SDK kurun.'
}

$projectPath = Join-Path $PSScriptRoot 'GoruntuIslemeOdevleri.csproj'
dotnet restore $projectPath
dotnet build $projectPath --configuration Release
dotnet run --project $projectPath --configuration Release
