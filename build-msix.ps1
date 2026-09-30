<#
.SYNOPSIS
    Builds Swifttill, the Microsoft Store (MSIX) edition of the POS.

.DESCRIPTION
    1. Publishes POSApp.UI self-contained for win-x64 (Release).
    2. Strips anything that must never ship in a public package (Firebase keys, .pdb, certs).
    3. Generates the Store tile/logo images from POSApp.UI\app-icon.png.
    4. Fills packaging\AppxManifest.template.xml from packaging\store-identity.json.
    5. Packs dist\Swifttill-<version>.msix with makeappx (Windows SDK).
    6. Optionally signs it with a local self-signed certificate so you can sideload-test it.

    The same codebase is the Store edition automatically when it runs from an MSIX package
    (see EditionService): Lite tier, Pro add-on via the Store, no licence codes, no pharmacy,
    no cloud backup.

    Upload the UNSIGNED .msix to Partner Center — the Store signs it for you.

.PARAMETER Version
    Four-part package version. The Store requires the last part to be 0 (e.g. 1.0.3.0) and
    every new submission to be higher than the previous one.

.PARAMETER Sign
    Also produce a self-signed copy (…-sideload.msix) for testing on this PC.

.PARAMETER Install
    With -Sign: install the sideload package on this PC (needs the test certificate trusted —
    the script prints the one-time admin command).

.EXAMPLE
    .\build-msix.ps1 -Version 1.0.0.0
.EXAMPLE
    .\build-msix.ps1 -Version 1.0.1.0 -Sign -Install
#>
[CmdletBinding()]
param(
    [string]$Version = '1.0.0.0',
    [switch]$Sign,
    [switch]$Install
)

$ErrorActionPreference = 'Stop'

$root       = $PSScriptRoot
$project    = Join-Path $root 'POSApp.UI\POSApp.UI.csproj'
$packDir    = Join-Path $root 'packaging'
$workDir    = Join-Path $root 'publish\msix'
$appDir     = Join-Path $workDir 'app'
$distDir    = Join-Path $root 'dist'
$iconSource = Join-Path $root 'POSApp.UI\app-icon.png'

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }

# ── 0. Inputs ──────────────────────────────────────────────────────────────────
Write-Step "Checking inputs"

if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') {
    throw "Version must look like 1.2.3.0 (four parts, last part 0 for the Store). Got '$Version'."
}

$identity = Get-Content (Join-Path $packDir 'store-identity.json') -Raw | ConvertFrom-Json
foreach ($key in 'IdentityName', 'Publisher', 'PublisherDisplayName', 'DisplayName', 'Description') {
    if ([string]::IsNullOrWhiteSpace($identity.$key)) { throw "store-identity.json is missing '$key'." }
}
$isPlaceholder = $identity.IdentityName -like '*PLACEHOLDER*' -or $identity.Publisher -like '*PLACEHOLDER*'
if ($isPlaceholder) {
    Write-Warning "store-identity.json still has PLACEHOLDER values. The package is fine for local testing, but Partner Center will reject it until you paste in your reserved identity."
}

# Pro subscription add-ons are recognised by Product ID prefix — must match the app.
$editionSrc = Get-Content (Join-Path $root 'POSApp.Infrastructure\Services\EditionService.cs') -Raw
if ($editionSrc -notmatch "ProProductIdPrefix\s*=\s*""$([regex]::Escape($identity.ProProductIdPrefix))""") {
    throw "ProProductIdPrefix in store-identity.json ('$($identity.ProProductIdPrefix)') does not match EditionService.ProProductIdPrefix."
}

# Windows SDK tools (newest installed version)
$kitBin = 'C:\Program Files (x86)\Windows Kits\10\bin'
$sdk = Get-ChildItem $kitBin -Directory -ErrorAction SilentlyContinue |
       Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' -and (Test-Path (Join-Path $_.FullName 'x64\makeappx.exe')) } |
       Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $sdk) { throw "makeappx.exe not found. Install the Windows 10/11 SDK (Visual Studio Installer → Individual components)." }
$makeappx = Join-Path $sdk.FullName 'x64\makeappx.exe'
$signtool = Join-Path $sdk.FullName 'x64\signtool.exe'
Write-Host "Windows SDK $($sdk.Name)"

# ── 1. Publish ─────────────────────────────────────────────────────────────────
Write-Step "Publishing POSApp.UI (Release, win-x64, self-contained)"
if (Test-Path $workDir) { Remove-Item $workDir -Recurse -Force }
New-Item -ItemType Directory -Force $appDir | Out-Null

$fileVersion = $Version
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:Version=$fileVersion -p:FileVersion=$fileVersion `
    -o $appDir --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

# ── 2. Strip secrets and debug files ───────────────────────────────────────────
Write-Step "Removing files that must not ship in a public package"
$forbidden = @('firebase-credentials.json', '*.pfx', '*.snk', '*.pdb', 'posapp.db*', 'sync.log', 'cloudbackup.state')
foreach ($pattern in $forbidden) {
    Get-ChildItem $appDir -Recurse -Filter $pattern -File | ForEach-Object {
        Write-Host "  removed $($_.FullName.Substring($appDir.Length + 1))"
        Remove-Item $_.FullName -Force
    }
}
$leak = Get-ChildItem $appDir -Recurse -File | Where-Object { $_.Name -match 'credential|secret|\.pfx$|private.?key' }
if ($leak) { throw "Refusing to package possible secrets: $($leak.Name -join ', ')" }

# ── 3. Visual assets ───────────────────────────────────────────────────────────
Write-Step "Generating Store logos from app-icon.png"
Add-Type -AssemblyName System.Drawing
$assetsDir = Join-Path $appDir 'Assets'
New-Item -ItemType Directory -Force $assetsDir | Out-Null

$src = [System.Drawing.Image]::FromFile($iconSource)
try {
    # The source art is portrait with the register in the middle — crop a centred square.
    $side = [int]([Math]::Min($src.Width, $src.Height) * 0.72)
    $crop = New-Object System.Drawing.Rectangle ([int](($src.Width - $side) / 2)), ([int](($src.Height - $side) / 2 + $src.Height * 0.02)), $side, $side
    $bg   = $src.GetPixel(10, 10)

    function New-Asset([string]$name, [int]$w, [int]$h, [double]$scale = 1.0) {
        $bmp = New-Object System.Drawing.Bitmap $w, $h
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.Clear($bg)
        $size = [int]([Math]::Min($w, $h) * $scale)
        $dest = New-Object System.Drawing.Rectangle ([int](($w - $size) / 2)), ([int](($h - $size) / 2)), $size, $size
        $g.DrawImage($src, $dest, $crop, [System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()
        $bmp.Save((Join-Path $assetsDir $name), [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    }

    New-Asset 'StoreLogo.png'           50  50
    New-Asset 'Square44x44Logo.png'     44  44
    New-Asset 'Square71x71Logo.png'     71  71
    New-Asset 'Square150x150Logo.png'  150 150
    New-Asset 'Square310x310Logo.png'  310 310
    New-Asset 'Wide310x150Logo.png'    310 150
    New-Asset 'SplashScreen.png'       620 300 0.9
    # Unplated taskbar icon variant (avoids the coloured plate around the taskbar icon)
    New-Asset 'Square44x44Logo.targetsize-44_altform-unplated.png' 44 44
}
finally { $src.Dispose() }

# ── 4. Manifest ────────────────────────────────────────────────────────────────
Write-Step "Writing AppxManifest.xml"
$manifest = Get-Content (Join-Path $packDir 'AppxManifest.template.xml') -Raw
$tokens = @{
    IdentityName         = $identity.IdentityName
    Publisher            = $identity.Publisher
    PublisherDisplayName = $identity.PublisherDisplayName
    DisplayName          = $identity.DisplayName
    Description          = $identity.Description
    Version              = $Version
}
foreach ($k in $tokens.Keys) {
    $escaped = [System.Security.SecurityElement]::Escape([string]$tokens[$k])
    $manifest = $manifest.Replace("{{$k}}", $escaped)
}
if ($manifest -match '\{\{\w+\}\}') { throw "Unfilled manifest token: $($Matches[0])" }
[System.IO.File]::WriteAllText((Join-Path $appDir 'AppxManifest.xml'), $manifest, (New-Object System.Text.UTF8Encoding $false))

# ── 5. Pack ────────────────────────────────────────────────────────────────────
Write-Step "Packing MSIX"
New-Item -ItemType Directory -Force $distDir | Out-Null
$msix = Join-Path $distDir "Swifttill-$Version.msix"
& $makeappx pack /d $appDir /p $msix /o | Out-Host
if ($LASTEXITCODE -ne 0) { throw "makeappx failed." }
Write-Host "Store upload package: $msix" -ForegroundColor Green

# ── 6. Optional self-signed copy for local testing ─────────────────────────────
if ($Sign) {
    Write-Step "Signing a sideload copy with a local test certificate"
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $identity.Publisher -and $_.NotAfter -gt (Get-Date) } | Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $identity.Publisher `
            -KeyUsage DigitalSignature -FriendlyName 'Swifttill MSIX test signing' `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
        Write-Host "Created test certificate $($cert.Thumbprint)"
    }
    $cer = Join-Path $distDir 'Swifttill-TestSigning.cer'
    Export-Certificate -Cert $cert -FilePath $cer | Out-Null

    $sideload = Join-Path $distDir "Swifttill-$Version-sideload.msix"
    Copy-Item $msix $sideload -Force
    & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $sideload | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "signtool failed." }
    Write-Host "Sideload package: $sideload" -ForegroundColor Green

    $trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople -ErrorAction SilentlyContinue | Where-Object Thumbprint -eq $cert.Thumbprint
    if (-not $trusted) {
        Write-Host "`nOne-time step to trust the test certificate (run in an ADMIN PowerShell):" -ForegroundColor Yellow
        Write-Host "  Import-Certificate -FilePath `"$cer`" -CertStoreLocation Cert:\LocalMachine\TrustedPeople"
    }

    if ($Install) {
        if (-not $trusted) { throw "Trust the test certificate first (command above), then re-run with -Install." }
        Add-AppxPackage -Path $sideload -ForceUpdateFromAnyVersion
        Write-Host "Installed. Start 'Swifttill' from the Start menu." -ForegroundColor Green
    }
}

Write-Host "`nNext: run the Windows App Certification Kit on the package before submitting:" -ForegroundColor Cyan
Write-Host "  & 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe' test -appxpackagepath `"$msix`" -reportoutputpath `"$distDir\wack-report.xml`""
