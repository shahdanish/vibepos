@echo off
REM Double-click this file to build a fresh dist\ShahJeePOS-Setup.exe
REM To bump the version instead, run from a terminal:  Build-Installer.cmd 3.1
setlocal

if "%~1"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1" -Version %1
)

echo.
pause
