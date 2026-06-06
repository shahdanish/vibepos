<#
.SYNOPSIS
    One-command build of a per-client Shah Jee POS installer.

.DESCRIPTION
    1. Prompts you to pick an existing client or create a new one.
    2. Injects that client's appsettings.json + firebase-credentials.json
       into the publish directory so they are bundled inside the installer.
    3. Publishes the WPF app as a self-contained single-file exe.
    4. Compiles the Inno Setup installer.
    5. Saves the output as  dist\ShahJeePOS-{ClientSlug}-Setup.exe

.PARAMETER Version
    New version string to stamp into the installer (e.g. "3.1"). If omitted,
    the current version in the .iss file is kept.

.PARAMETER Client
    Client slug to skip the interactive prompt (e.g. "ABC-Super-Store").
    Must match a folder name under clients\.

.EXAMPLE
    .\build-installer.ps1
    Interactive — asks which client, then builds.

.EXAMPLE
    .\build-installer.ps1 -Version 3.1
    Bumps version, then asks which client and builds.

.EXAMPLE
    .\build-installer.ps1 -Client "ABC-Super-Store"
    Builds for ABC-Super-Store without any prompts.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Client
)

$ErrorActionPreference = 'Stop'

$root        = $PSScriptRoot
$project     = Join-Path $root 'POSApp.UI\POSApp.UI.csproj'
$issScript   = Join-Path $root 'installer\ShahJeePOS.iss'
$publishDir  = Join-Path $root 'publish\installer'
$distDir     = Join-Path $root 'dist'
$clientsDir  = Join-Path $root 'clients'

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }

# ── 0. Client selection ────────────────────────────────────────────────────────
Write-Step "Client selection"

# Discover existing client profiles
$existingClients = @()
if (Test-Path $clientsDir) {
    $existingClients = @(Get-ChildItem $clientsDir -Directory | ForEach-Object {
        $cfgPath = Join-Path $_.FullName 'config.json'
        if (Test-Path $cfgPath) {
            $cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
            $hasCreds = Test-Path (Join-Path $_.FullName 'firebase-credentials.json')
            [PSCustomObject]@{
                Slug    = $_.Name
                Name    = $cfg.name
                Path    = $_.FullName
                HasCreds = $hasCreds
            }
        }
    } | Where-Object { $_ })
}

$selectedClient = $null

# If -Client was passed on the command line, find it directly
if ($Client) {
    $selectedClient = $existingClients | Where-Object { $_.Slug -eq $Client } | Select-Object -First 1
    if (-not $selectedClient) { throw "Client '$Client' not found in clients\ directory." }
    Write-Host "    Using client: $($selectedClient.Name)  [$($selectedClient.Slug)]"
} else {
    # Interactive prompt
    Write-Host ""
    if ($existingClients.Count -gt 0) {
        Write-Host "  Existing clients:" -ForegroundColor Yellow
        for ($i = 0; $i -lt $existingClients.Count; $i++) {
            $creds = if ($existingClients[$i].HasCreds) { "[Firebase: YES]" } else { "[Firebase: NO - offline only]" }
            Write-Host ("  [{0}] {1,-30} {2}" -f ($i + 1), $existingClients[$i].Name, $creds)
        }
        Write-Host ""
    } else {
        Write-Host "  No client profiles found yet." -ForegroundColor DarkGray
    }

    Write-Host "  [N] Create new client" -ForegroundColor Green
    Write-Host ""
    $choice = (Read-Host "  Enter number or N").Trim()

    if ($choice -eq 'N' -or $choice -eq 'n') {
        # ── Create new client ──────────────────────────────────────────────────
        Write-Host ""
        $clientName = (Read-Host "  Shop / client name (e.g. ABC Super Store)").Trim()
        if (-not $clientName) { throw "Client name cannot be empty." }

        # Generate a filesystem-safe slug from the name
        $slug = $clientName -replace '[^\w\s-]', '' -replace '\s+', '-' -replace '-+', '-'
        $slug = $slug.Trim('-')

        $clientPath = Join-Path $clientsDir $slug
        New-Item -ItemType Directory -Path $clientPath -Force | Out-Null

        @{ name = $clientName } | ConvertTo-Json | Set-Content (Join-Path $clientPath 'config.json') -Encoding UTF8

        Write-Host "  Created profile: clients\$slug" -ForegroundColor Green

        # Optionally copy firebase credentials via file browser
        Write-Host ""
        Write-Host "  A file browser will open - select the client's firebase-credentials.json." -ForegroundColor Yellow
        Write-Host "  (Close the dialog without selecting to skip - app will work offline.)"
        Write-Host ""

        Add-Type -AssemblyName System.Windows.Forms
        $dlg = New-Object System.Windows.Forms.OpenFileDialog
        $dlg.Title            = "Select firebase-credentials.json for: $clientName"
        $dlg.Filter           = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        $dlg.InitialDirectory = [Environment]::GetFolderPath('Desktop')
        $dlg.FileName         = "firebase-credentials.json"

        # ShowDialog needs a parent handle; use a hidden form so the dialog stays on top
        $owner = New-Object System.Windows.Forms.Form
        $owner.TopMost = $true
        $owner.WindowState = 'Minimized'
        $owner.ShowInTaskbar = $false
        $owner.Show()
        $owner.Hide()

        $result  = $dlg.ShowDialog($owner)
        $owner.Dispose()
        $credSrc = if ($result -eq [System.Windows.Forms.DialogResult]::OK) { $dlg.FileName } else { '' }

        if ($credSrc) {
            $destCred = Join-Path $clientPath 'firebase-credentials.json'
            Copy-Item $credSrc $destCred -Force
            Write-Host "  Credentials saved to clients\$slug\firebase-credentials.json" -ForegroundColor Green
            $hasCreds = $true
        } else {
            Write-Host "  Skipped - installer will not include Firebase credentials." -ForegroundColor DarkYellow
            $hasCreds = $false
        }

        $selectedClient = [PSCustomObject]@{
            Slug     = $slug
            Name     = $clientName
            Path     = $clientPath
            HasCreds = $hasCreds
        }
    } else {
        # ── Select existing client ─────────────────────────────────────────────
        $idx = 0
        if (-not [int]::TryParse($choice, [ref]$idx)) { throw "Invalid input: '$choice'" }
        $idx -= 1
        if ($idx -lt 0 -or $idx -ge $existingClients.Count) { throw "Selection out of range." }
        $selectedClient = $existingClients[$idx]
        Write-Host "  Selected: $($selectedClient.Name)" -ForegroundColor Green
    }
}

# ── 1. Optional version bump ───────────────────────────────────────────────────
if ($Version) {
    Write-Step "Setting installer version to $Version"
    $iss = Get-Content $issScript -Raw
    $iss = [regex]::Replace($iss, '(#define\s+MyAppVersion\s+")[^"]*(")', "`${1}$Version`${2}")
    Set-Content -Path $issScript -Value $iss -Encoding UTF8
}

$verMatch    = [regex]::Match((Get-Content $issScript -Raw), '#define\s+MyAppVersion\s+"([^"]*)"')
$builtVersion = if ($verMatch.Success) { $verMatch.Groups[1].Value } else { 'unknown' }

# ── 2. Locate ISCC.exe ─────────────────────────────────────────────────────────
Write-Step "Locating Inno Setup compiler (ISCC.exe)"
$iscc = $null
$cmd  = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($cmd) { $iscc = $cmd.Source }
if (-not $iscc) {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) { throw "ISCC.exe not found. Install Inno Setup 6 from https://jrsoftware.org/isdl.php" }
Write-Host "    Using: $iscc"

# ── 3. Publish the self-contained single-file build ───────────────────────────
Write-Step "Publishing self-contained single-file build"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }

# ── 4. Inject client files into publish directory ─────────────────────────────
Write-Step "Injecting client files for: $($selectedClient.Name)"

# Generate appsettings.json for this client
$appSettings = @{
    Client   = @{ Name = $selectedClient.Name }
    Firebase = @{ CredentialsPath = "firebase-credentials.json" }
} | ConvertTo-Json -Depth 3
Set-Content (Join-Path $publishDir 'appsettings.json') -Value $appSettings -Encoding UTF8
Write-Host "    appsettings.json  → Client.Name = $($selectedClient.Name)"

# Copy Firebase credentials if available
$credFile = Join-Path $selectedClient.Path 'firebase-credentials.json'
if (Test-Path $credFile) {
    Copy-Item $credFile (Join-Path $publishDir 'firebase-credentials.json') -Force
    Write-Host "    firebase-credentials.json  → copied"
} else {
    Write-Host "    firebase-credentials.json  → not found, skipping (offline-only install)" -ForegroundColor DarkYellow
}

# ── 5. Compile the installer ───────────────────────────────────────────────────
Write-Step "Compiling installer with Inno Setup"
$outputBaseName = "ShahJeePOS-$($selectedClient.Slug)-Setup"
& $iscc $issScript "/F$outputBaseName"
if ($LASTEXITCODE -ne 0) { throw "ISCC failed (exit $LASTEXITCODE)." }

# ── 6. Done ────────────────────────────────────────────────────────────────────
$outputExe = Join-Path $distDir "$outputBaseName.exe"
if (Test-Path $outputExe) {
    $size = [math]::Round((Get-Item $outputExe).Length / 1MB, 1)
    Write-Host "`n[OK] Installer built successfully." -ForegroundColor Green
    Write-Host "     Client  : $($selectedClient.Name)"
    Write-Host "     Version : $builtVersion"
    Write-Host "     Output  : $outputExe  ($size MB)"
} else {
    throw "Build reported success but $outputExe was not found."
}
