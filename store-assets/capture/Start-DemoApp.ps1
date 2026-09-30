<#
.SYNOPSIS
    Launches SwiftTill against a fresh copy of the DEMO database — never the real one.

.DESCRIPTION
    No source or config file is changed. The app already supports two environment variables:
      POSAPP_DATA_DIR  -> where posapp.db, logs and shop settings live (POSApp.Core/Services/AppPaths.cs)
      POSAPP_EDITION   -> 'pro' makes a Debug build behave as the Store Pro edition (EditionService.cs)
    They are set only for the launched process.

    Every launch copies seed\demo.sqlite over capture\runtime\posapp.db, so screenshots
    always start from the same pristine demo data.
#>
param(
    [string]$Configuration = 'Debug',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$assets  = Split-Path $PSScriptRoot -Parent
$root    = Split-Path $assets -Parent
$runtime = Join-Path $PSScriptRoot 'runtime'
$demoDb  = Join-Path $assets 'seed\demo.sqlite'

if (-not (Test-Path $demoDb)) { throw "Missing $demoDb. Run: dotnet run --project store-assets/seed/DemoSeed" }

$real = Join-Path $env:LOCALAPPDATA 'ShahJeePOS'
if ([IO.Path]::GetFullPath($runtime).TrimEnd('\') -ieq [IO.Path]::GetFullPath($real).TrimEnd('\')) {
    throw 'Refusing to run: runtime folder is the real data folder.'
}

if (-not $NoBuild) {
    dotnet build (Join-Path $root 'POSApp.UI\POSApp.UI.csproj') -c $Configuration --nologo -v q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
$exe = Get-ChildItem (Join-Path $root "POSApp.UI\bin\$Configuration") -Recurse -Filter 'POSApp.UI.exe' |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $exe) { throw 'POSApp.UI.exe not found — build first.' }

if (Test-Path $runtime) { Remove-Item $runtime -Recurse -Force }
New-Item -ItemType Directory -Force $runtime | Out-Null
Copy-Item $demoDb (Join-Path $runtime 'posapp.db')
Copy-Item (Join-Path $assets 'seed\demo-settings\*.json') $runtime

$psi = New-Object System.Diagnostics.ProcessStartInfo $exe.FullName
$psi.WorkingDirectory = $runtime          # no legacy posapp.db here, so nothing real is ever copied in
$psi.UseShellExecute  = $false
$psi.EnvironmentVariables['POSAPP_DATA_DIR'] = $runtime
$psi.EnvironmentVariables['POSAPP_EDITION']  = 'pro'
$proc = [System.Diagnostics.Process]::Start($psi)
Write-Host "SwiftTill started (pid $($proc.Id)) with demo data in $runtime"
Write-Host 'Logins: admin / demo1234   cashier / demo1234'
$proc
