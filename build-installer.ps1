<#
.SYNOPSIS
    One-command build of the Shah Jee POS installer (dist\ShahJeePOS-Setup.exe).

.DESCRIPTION
    Publishes the WPF app as a self-contained single-file exe, then compiles the
    Inno Setup installer. Locates ISCC.exe automatically. Optionally bumps the
    version number recorded in installer\ShahJeePOS.iss.

.PARAMETER Version
    New version string to stamp into the installer (e.g. "3.1"). If omitted, the
    current version in the .iss file is kept.

.EXAMPLE
    .\build-installer.ps1
    Rebuilds the installer with the current version.

.EXAMPLE
    .\build-installer.ps1 -Version 3.1
    Bumps the version to 3.1 and rebuilds the installer.
#>
[CmdletBinding()]
param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'

# Always run relative to this script's folder, so it works no matter where it's called from.
$root        = $PSScriptRoot
$project     = Join-Path $root 'POSApp.UI\POSApp.UI.csproj'
$issScript   = Join-Path $root 'installer\ShahJeePOS.iss'
$publishDir  = Join-Path $root 'publish\installer'
$outputExe   = Join-Path $root 'dist\ShahJeePOS-Setup.exe'

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }

# --- 0. Optional version bump -------------------------------------------------
if ($Version) {
    Write-Step "Setting installer version to $Version"
    $iss = Get-Content $issScript -Raw
    $iss = [regex]::Replace($iss, '(#define\s+MyAppVersion\s+")[^"]*(")', "`${1}$Version`${2}")
    Set-Content -Path $issScript -Value $iss -Encoding UTF8
}

# Read the version that will actually be built (for the final summary).
$verMatch = [regex]::Match((Get-Content $issScript -Raw), '#define\s+MyAppVersion\s+"([^"]*)"')
$builtVersion = if ($verMatch.Success) { $verMatch.Groups[1].Value } else { 'unknown' }

# --- 1. Locate ISCC.exe (Inno Setup compiler) --------------------------------
Write-Step "Locating Inno Setup compiler (ISCC.exe)"
$iscc = $null
$cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($cmd) { $iscc = $cmd.Source }
if (-not $iscc) {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) {
    throw "ISCC.exe not found. Install Inno Setup 6 from https://jrsoftware.org/isdl.php"
}
Write-Host "    Using: $iscc"

# --- 2. Publish the self-contained single-file build -------------------------
Write-Step "Publishing self-contained single-file build"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }

# --- 3. Compile the installer ------------------------------------------------
Write-Step "Compiling installer with Inno Setup"
& $iscc $issScript
if ($LASTEXITCODE -ne 0) { throw "ISCC failed (exit $LASTEXITCODE)." }

# --- 4. Done -----------------------------------------------------------------
if (Test-Path $outputExe) {
    $size = [math]::Round((Get-Item $outputExe).Length / 1MB, 1)
    Write-Host "`n[OK] Installer built successfully." -ForegroundColor Green
    Write-Host "     Version : $builtVersion"
    Write-Host "     Output  : $outputExe  ($size MB)"
} else {
    throw "Build reported success but $outputExe was not found."
}
