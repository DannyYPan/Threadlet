@echo off
setlocal
call "%~dp0vm-config.cmd"
if not defined THREADLET_VMRUN exit /b 0
if not defined THREADLET_VMX exit /b 0
if not exist "%THREADLET_VMRUN%" exit /b 1
if not exist "%THREADLET_VMX%" exit /b 1
"%THREADLET_VMRUN%" start "%THREADLET_VMX%" nogui
exit /b %errorlevel%
