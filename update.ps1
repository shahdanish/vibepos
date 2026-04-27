# POS System Update Script - Version 3.0
# This script automates the update process

param(
    [string]$InstallPath = "C:\Program Files\Shah Jee POS",
    [switch]$SkipBackup = $false,
    [switch]$AutoStart = $true
)

# Color functions
function Write-Success { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }
function Write-Warning { param($msg) Write-Host $msg -ForegroundColor Yellow }
function Write-Error { param($msg) Write-Host $msg -ForegroundColor Red }

# Banner
Write-Host "`n╔════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   Shah Jee POS - Update to v3.0      ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════╝`n" -ForegroundColor Cyan

# Configuration
$ProjectRoot = "e:\AI Projects\POSApp"
$PublishPath = "$ProjectRoot\publish\POSApp_v3"
$BackupPath = "$env:USERPROFILE\Desktop\POSApp_Backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

Write-Info "Configuration:"
Write-Host "  Installation Path: $InstallPath" -ForegroundColor White
Write-Host "  Project Root: $ProjectRoot" -ForegroundColor White
Write-Host "  Backup Path: $BackupPath`n" -ForegroundColor White

# Check if installation exists
if (-not (Test-Path $InstallPath)) {
    Write-Error "❌ Installation not found at: $InstallPath"
    Write-Warning "Please specify correct installation path with -InstallPath parameter"
    Write-Host "`nExample: .\update.ps1 -InstallPath 'D:\POSApp'" -ForegroundColor Yellow
    exit 1
}

# Step 1: Stop application
Write-Info "Step 1/7: Stopping application..."
try {
    $process = Get-Process -Name "POSApp.UI" -ErrorAction SilentlyContinue
    if ($process) {
        Stop-Process -Name "POSApp.UI" -Force
        Start-Sleep -Seconds 2
        Write-Success "  ✅ Application stopped"
    } else {
        Write-Success "  ✅ Application not running"
    }
} catch {
    Write-Warning "  ⚠️ Could not stop application (may not be running)"
}

# Step 2: Backup
if (-not $SkipBackup) {
    Write-Info "`nStep 2/7: Creating backup..."
    try {
        Copy-Item -Path $InstallPath -Destination $BackupPath -Recurse -Force
        Write-Success "  ✅ Backup created at: $BackupPath"
        
        # Verify backup
        $backupSize = (Get-ChildItem -Path $BackupPath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
        Write-Host "  Backup size: $([math]::Round($backupSize, 2)) MB" -ForegroundColor Gray
    } catch {
        Write-Error "  ❌ Backup failed: $_"
        Write-Warning "  Continue anyway? (Y/N)"
        $response = Read-Host
        if ($response -ne 'Y') { exit 1 }
    }
} else {
    Write-Warning "`nStep 2/7: Backup skipped (use -SkipBackup to skip)"
}

# Step 3: Build new version
Write-Info "`nStep 3/7: Building new version..."
try {
    Set-Location $ProjectRoot
    
    # Clean old publish folder
    if (Test-Path $PublishPath) {
        Remove-Item -Path $PublishPath -Recurse -Force
    }
    
    Write-Host "  Building self-contained executable..." -ForegroundColor Gray
    
    $buildOutput = dotnet publish POSApp.UI -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $PublishPath 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "  ✅ Build completed successfully"
        
        # Check file size
        $exeSize = (Get-Item "$PublishPath\POSApp.UI.exe").Length / 1MB
        Write-Host "  Executable size: $([math]::Round($exeSize, 2)) MB" -ForegroundColor Gray
    } else {
        Write-Error "  ❌ Build failed!"
        Write-Host $buildOutput -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Error "  ❌ Build error: $_"
    exit 1
}

# Step 4: Verify new build
Write-Info "`nStep 4/7: Verifying build..."
$requiredFiles = @("POSApp.UI.exe")
$allFilesExist = $true

foreach ($file in $requiredFiles) {
    if (Test-Path "$PublishPath\$file") {
        Write-Success "  ✅ $file found"
    } else {
        Write-Error "  ❌ $file missing!"
        $allFilesExist = $false
    }
}

if (-not $allFilesExist) {
    Write-Error "`nBuild verification failed. Aborting update."
    exit 1
}

# Step 5: Update files
Write-Info "`nStep 5/7: Updating installation..."
try {
    # Backup database one more time (extra safety)
    if (Test-Path "$InstallPath\posapp.db") {
        Copy-Item "$InstallPath\posapp.db" "$InstallPath\posapp.db.backup" -Force
        Write-Success "  ✅ Database backup created"
    }
    
    # Update executable
    Copy-Item "$PublishPath\POSApp.UI.exe" "$InstallPath\POSApp.UI.exe" -Force
    Write-Success "  ✅ Executable updated"
    
    # Copy other necessary files (if any)
    # Settings file will auto-create in AppData
    
    Write-Success "`n  ✅ Installation updated successfully!"
} catch {
    Write-Error "  ❌ Update failed: $_"
    Write-Warning "`n  Rollback available at: $BackupPath"
    exit 1
}

# Step 6: Verify installation
Write-Info "`nStep 6/7: Verifying installation..."
$verificationOK = $true

if (Test-Path "$InstallPath\POSApp.UI.exe") {
    $newExeSize = (Get-Item "$InstallPath\POSApp.UI.exe").Length / 1MB
    Write-Success "  ✅ New executable installed ($([math]::Round($newExeSize, 2)) MB)"
} else {
    Write-Error "  ❌ Executable not found!"
    $verificationOK = $false
}

if (Test-Path "$InstallPath\posapp.db") {
    $dbSize = (Get-Item "$InstallPath\posapp.db").Length / 1KB
    Write-Success "  ✅ Database intact ($([math]::Round($dbSize, 2)) KB)"
} else {
    Write-Warning "  ⚠️ Database not found (will be created on first run)"
}

if (Test-Path "$InstallPath\posapp.db.backup") {
    Write-Success "  ✅ Database backup available"
}

# Step 7: Launch application
if ($AutoStart -and $verificationOK) {
    Write-Info "`nStep 7/7: Starting application..."
    try {
        Start-Process "$InstallPath\POSApp.UI.exe"
        Start-Sleep -Seconds 3
        
        $process = Get-Process -Name "POSApp.UI" -ErrorAction SilentlyContinue
        if ($process) {
            Write-Success "  ✅ Application started successfully (PID: $($process.Id))"
        } else {
            Write-Warning "  ⚠️ Application may not have started. Please launch manually."
        }
    } catch {
        Write-Warning "  ⚠️ Could not start application automatically: $_"
        Write-Info "  Please start manually: $InstallPath\POSApp.UI.exe"
    }
} else {
    Write-Info "`nStep 7/7: Auto-start disabled"
    Write-Host "  To start manually: " -NoNewline -ForegroundColor Gray
    Write-Host "$InstallPath\POSApp.UI.exe" -ForegroundColor White
}

# Summary
Write-Host "`n╔════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║     Update Completed Successfully!    ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════╝`n" -ForegroundColor Green

Write-Info "📋 Update Summary:"
Write-Host "  Version: 3.0" -ForegroundColor White
Write-Host "  Installation: $InstallPath" -ForegroundColor White
Write-Host "  Backup: $BackupPath" -ForegroundColor White
Write-Host "  Settings: %AppData%\POSApp\settings.json" -ForegroundColor White

Write-Info "`n🎯 New Features Available:"
Write-Host "  • Settings now persist (Auto Print, Small Bill, etc.)" -ForegroundColor White
Write-Host "  • Purchase price box visible with toggle" -ForegroundColor White
Write-Host "  • Barcode auto-fills when Product ID generated" -ForegroundColor White
Write-Host "  • Professional notification messages with emoji" -ForegroundColor White

Write-Info "`n✅ Verification Steps:"
Write-Host "  1. Open Sale window → Check purchase price box (bottom-right)" -ForegroundColor White
Write-Host "  2. Toggle 'Auto Print' → Close → Reopen → Should still be checked" -ForegroundColor White
Write-Host "  3. Products → Add product → See professional success message" -ForegroundColor White
Write-Host "  4. Products → Generate ID → Barcode auto-fills" -ForegroundColor White

Write-Info "`n⚠️ Rollback (if needed):"
Write-Host "  Copy files from: $BackupPath" -ForegroundColor Yellow

Write-Host "`n🎉 Update complete! Enjoy the new features!`n" -ForegroundColor Cyan

# Open backup location
$openBackup = Read-Host "Open backup location? (Y/N)"
if ($openBackup -eq 'Y') {
    explorer $BackupPath
}
