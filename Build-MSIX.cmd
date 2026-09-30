@echo off
REM Builds the Microsoft Store (MSIX) package into dist\. See build-msix.ps1 for options.
REM Usage: Build-MSIX.cmd 1.0.0.0
pushd "%~dp0"
if "%~1"=="" (
  powershell -NoProfile -ExecutionPolicy Bypass -File build-msix.ps1
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -File build-msix.ps1 -Version %1
)
popd
pause
