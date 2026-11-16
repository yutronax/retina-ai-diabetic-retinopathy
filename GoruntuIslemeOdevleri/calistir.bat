@echo off
chcp 65001 >nul
rem Goruntu Isleme Laboratuvari - son kodu derler ve uygulamayi acar.
setlocal
cd /d "%~dp0"
set "DOTNET=dotnet"
if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
  set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
  set "DOTNET=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
)
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
echo Derleniyor...
"%DOTNET%" build GoruntuIslemeOdevleri.csproj -c Release -v q -nologo -nodeReuse:false -p:UseSharedCompilation=false
if errorlevel 1 (
  echo.
  echo Derleme basarisiz. .NET 8 SDK kurulu mu? https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
start "" "%~dp0bin\Release\net8.0-windows\GoruntuIslemeOdevleri.exe"
endlocal
