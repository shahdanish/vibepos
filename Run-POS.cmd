@echo off
REM Development launcher — always compiles from source so every code change is applied.
REM For a production installer use Build-Installer.cmd instead.
pushd "%~dp0"
dotnet run --project POSApp.UI
popd
pause
