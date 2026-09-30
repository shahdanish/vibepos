<#
.SYNOPSIS
    Captures the Microsoft Store screenshots from the REAL running app, on demo data.

.DESCRIPTION
    1. Re-seeds nothing by itself — run the seeder first so dates are current:
         dotnet run --project store-assets/seed/DemoSeed
    2. Launches the app through Start-DemoApp.ps1 (fresh copy of seed\demo.sqlite, Store Pro edition).
    3. Drives it with Windows UI Automation (keyboard/clicks a cashier would make) and grabs each
       window's visible frame at exactly 1920x1080 physical pixels. Nothing is drawn or pasted in.

    Keep the mouse still and don't switch windows while it runs (~1 minute); the captures are
    screen grabs, so anything on top of the app would appear in them.

.PARAMETER Out
    Output folder (default: store-assets\screenshots).
#>
param(
    [string]$Out = (Join-Path (Split-Path $PSScriptRoot -Parent) 'screenshots'),
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UiaHelpers.ps1')

$runtime = Join-Path $PSScriptRoot 'runtime'
$demoDb  = Join-Path (Split-Path $PSScriptRoot -Parent) 'seed\demo.sqlite'

$proc = & (Join-Path $PSScriptRoot 'Start-DemoApp.ps1') -NoBuild:$NoBuild | Select-Object -Last 1
$pidApp = $proc.Id
$substDrive = $null

function Open-Screen([string]$Top, [string]$Item, [string]$TitleLike) {
    # Wait until the previous screen has really gone (the main window is disabled while a
    # modal child is open, and its menu won't drop down).
    Wait-Until { @(Get-AppWindows $pidApp).Count -eq 1 } 15 'previous screen to close' | Out-Null
    $main = Get-TopWindow $pidApp 'Swifttill*'
    Invoke-MenuItem $pidApp $main $Top $Item
    $w = Wait-Window $pidApp $TitleLike
    Set-WindowFrame $w
    Start-Sleep -Milliseconds 800
    $w
}

try {
    # ── Login ────────────────────────────────────────────────────────────────
    $login = Wait-Window $pidApp 'Login*' 60
    [void][Win32]::SetForegroundWindow((Get-Hwnd $login))
    Set-Text (Find-El $login -AutomationId txtUsername) 'admin'
    (Find-El $login -AutomationId txtPassword).SetFocus()
    Send-Keys 'demo1234'
    Invoke-El (Find-El $login -Name 'Sign in' -ControlType Button)
    $main = Wait-Window $pidApp 'Swifttill*' 30
    Set-WindowFrame $main
    Start-Sleep 2

    # ── 01 Billing: scan six products like a barcode scanner (types code + Enter) ──
    $sale = Open-Screen '*Sales' '?? Sale' 'Sale -*'
    $edits = $sale.FindAll($script:TS::Descendants,
        (New-Object $script:Cond ($script:AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
    $scan = $edits[4]       # "Scan Barcode" field (order: name, mobile, date, note, scan, ...)
    foreach ($code in '2001000000173', '2001000000203', '2001000000258', '2001000000265',
                      '2001000000265', '2001000000012', '2001000000142') {
        $scan.SetFocus(); Send-Keys "$code{ENTER}"; Start-Sleep -Milliseconds 400
    }
    $edits[8].SetFocus(); Send-Keys '4000'      # "Cash received" -> change is calculated live
    $scan.SetFocus()
    Save-WindowShot $sale (Join-Path $Out '01-billing.png')
    Invoke-El (Find-El $sale -Name 'Close' -ControlType Button)
    Start-Sleep 1

    # ── 02 Receipt: the app's own receipt preview (Admin > Business Settings > Preview) ──
    $biz = Open-Screen '*Admin' '*Business Settings' 'Business Settings*'
    Invoke-El (Find-El $biz -Name '*Preview*' -ControlType TabItem)
    Start-Sleep 1
    Save-WindowShot $biz (Join-Path $Out '02-receipt.png')
    Close-Window $biz

    # ── 03 Inventory: main overview — top sellers + low-stock list ───────────
    Save-WindowShot $main (Join-Path $Out '03-inventory.png')

    # ── 04 Sales report: this month's totals ─────────────────────────────────
    $rep = Open-Screen '*Reports' '*Sales Report' 'Sales Report*'
    Invoke-El (Find-El $rep -Name 'This month' -ControlType Button)
    Start-Sleep 2
    Save-WindowShot $rep (Join-Path $Out '04-sales-report.png')
    Close-Window $rep

    # ── 05 Customers: khata ledger with the top balance selected ─────────────
    $led = Open-Screen '*Finance' '*Khata' 'Customer Ledger*'
    $row = $led.FindAll($script:TS::Descendants,
        (New-Object $script:Cond ($script:AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::DataItem))) |
        Select-Object -First 1
    Invoke-El $row
    Start-Sleep 1
    Invoke-El (Find-El $led -Name 'All' -ControlType Button)
    Start-Sleep 1
    Save-WindowShot $led (Join-Path $Out '05-customers.png')
    Close-Window $led

    # ── 06 Users ─────────────────────────────────────────────────────────────
    $usr = Open-Screen '*Admin' '*Users' 'User Management*'
    Save-WindowShot $usr (Join-Path $Out '06-users.png')
    Close-Window $usr

    # ── 07 Backup & restore (local). The default folder is under the Windows user's
    #    Documents, which would show the account name, so a temporary SUBST drive maps a folder
    #    inside capture\runtime and the app's own Browse dialog selects it. Two older backups
    #    are copies of the demo DB; the newest is made by the app's "Create Backup" button.
    $substDrive = ('B','R','T','X','Y') | Where-Object { -not (Test-Path "${_}:\") } | Select-Object -First 1
    if (-not $substDrive) { throw 'No free drive letter for SUBST' }
    $substRoot = Join-Path $runtime 'subst'
    $backupDir = Join-Path $substRoot 'Demo Mart Backups'
    New-Item -ItemType Directory -Force $backupDir | Out-Null
    foreach ($daysAgo in 14, 7) {
        $stamp = (Get-Date).Date.AddDays(-$daysAgo).AddHours(21).AddMinutes(5).ToString('yyyyMMdd_HHmmss')
        Copy-Item $demoDb (Join-Path $backupDir "posapp_backup_$stamp.db")
    }
    subst "${substDrive}:" $substRoot | Out-Null
    $target = "${substDrive}:\Demo Mart Backups"

    $bk = Open-Screen '*Admin' '*Backup' 'Backup*'
    Invoke-El (Find-El $bk -Name 'Browse*' -ControlType Button)
    $dlg = Wait-Until {
        Get-AppWindows $pidApp | Where-Object { $_.Current.ClassName -eq '#32770' } | Select-Object -First 1
    } 15 'folder picker'
    [void](Wait-Until { Find-El $dlg -AutomationId '1152' } 10 'folder name box')
    Select-DialogPath $dlg $target
    Start-Sleep 1
    Invoke-El (Find-El $bk -Name 'Create Backup' -ControlType Button)
    Start-Sleep 2
    # The app confirms with a message box; dismiss it so it isn't in the capture.
    $msg = Get-AppWindows $pidApp | Where-Object { $_.Current.ClassName -eq '#32770' } | Select-Object -First 1
    if ($msg) { $ok = Find-El $msg -Name 'OK' -ControlType Button; if ($ok) { Invoke-El $ok } else { Close-Window $msg } }
    Start-Sleep 1
    Save-WindowShot $bk (Join-Path $Out '07-backup.png')
    Close-Window $bk
}
finally {
    if ($substDrive) { subst "${substDrive}:" /d | Out-Null }
    if (-not $proc.HasExited) { $proc.CloseMainWindow() | Out-Null; Start-Sleep 2; if (-not $proc.HasExited) { $proc.Kill() } }
}
Write-Host "Screenshots written to $Out"
