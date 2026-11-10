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
if exist "%~dp0vm-config.cmd" call "%~dp0vm-config.cmd"
if defined THREADLET_VMRUN if defined THREADLET_VMX (
  if not exist "%THREADLET_VMRUN%" goto vmerror
  if not exist "%THREADLET_VMX%" goto vmerror
  "%THREADLET_VMRUN%" start "%THREADLET_VMX%" nogui
  if errorlevel 1 goto vmerror
)
"%DOTNET_EXE%" run --project "%~dp0Threadlet\Threadlet.csproj" --configuration Release
if errorlevel 1 pause
exit /b
:vmerror
echo VM startup failed. Check vm-config.cmd. Your VM has not been installed or changed.
pause
exit /b 1
