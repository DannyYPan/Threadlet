@echo off
setlocal
cd /d "%~dp0"
set "DOTNET_EXE=dotnet"
where dotnet >nul 2>nul
if errorlevel 1 (
  if exist "%ProgramFiles%\dotnet\dotnet.exe" (
    set "DOTNET_EXE=%ProgramFiles%\dotnet\dotnet.exe"
  ) else (
    echo Threadlet needs the .NET 8 SDK. Run Install-DotNet.bat first.
    pause
    exit /b 1
  )
)
"%DOTNET_EXE%" run --project "%~dp0Threadlet\Threadlet.csproj" --configuration Release
if errorlevel 1 pause
