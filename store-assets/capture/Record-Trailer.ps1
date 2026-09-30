<#
.SYNOPSIS
    Screen-records the REAL app (demo data) for the Store trailer.

.DESCRIPTION
    Launches SwiftTill via Start-DemoApp.ps1, logs in, then records the 1920x1080 region where
    every window is placed (top-left of the primary screen) with ffmpeg gdigrab, while UI
    Automation performs the actions at a watchable pace. Each scene's usable span is written to
    capture\runtime\trailer\scenes.json so src\trailer.py can cut out window-opening moments.

    Keep the mouse still and don't switch windows while it runs (~1.5 minutes).

    Needs ffmpeg: python -m pip install --target store-assets/tools imageio-ffmpeg
#>
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UiaHelpers.ps1')

$assets = Split-Path $PSScriptRoot -Parent
$ffmpeg = Get-ChildItem (Join-Path $assets 'tools\imageio_ffmpeg\binaries') -Filter 'ffmpeg*.exe' | Select-Object -First 1
if (-not $ffmpeg) { throw 'ffmpeg not found — python -m pip install --target store-assets/tools imageio-ffmpeg' }

$proc = & (Join-Path $PSScriptRoot 'Start-DemoApp.ps1') -NoBuild:$NoBuild | Select-Object -Last 1
$pidApp = $proc.Id
$outDir = Join-Path $PSScriptRoot 'runtime\trailer'
New-Item -ItemType Directory -Force $outDir | Out-Null
$raw = Join-Path $outDir 'raw.mp4'

$scenes = [ordered]@{}
$clock = [Diagnostics.Stopwatch]::new()
function Mark-Start([string]$Name) { $scenes[$Name] = [ordered]@{ start = $clock.Elapsed.TotalSeconds } }
function Mark-End([string]$Name)   { $scenes[$Name].end = $clock.Elapsed.TotalSeconds }

function Type-Slow([string]$Text, [int]$DelayMs = 45) {
    foreach ($ch in $Text.ToCharArray()) {
        $k = if ('+^%~(){}[]'.Contains($ch)) { "{$ch}" } else { "$ch" }
        [System.Windows.Forms.SendKeys]::SendWait($k)
        Start-Sleep -Milliseconds $DelayMs
    }
}

function Open-Screen([string]$Top, [string]$Item, [string]$TitleLike) {
    Wait-Until { @(Get-AppWindows $pidApp).Count -eq 1 } 15 'previous screen to close' | Out-Null
    $main = Get-TopWindow $pidApp 'Swifttill*'
    Invoke-MenuItem $pidApp $main $Top $Item
    $w = Wait-Window $pidApp $TitleLike
    Set-WindowFrame $w
    Start-Sleep -Milliseconds 900          # let it paint before the scene starts
    $w
}

$rec = $null
try {
    # ── Login (not recorded) ─────────────────────────────────────────────────
    $login = Wait-Window $pidApp 'Login*' 60
    [void][Win32]::SetForegroundWindow((Get-Hwnd $login))
    Set-Text (Find-El $login -AutomationId txtUsername) 'admin'
    (Find-El $login -AutomationId txtPassword).SetFocus()
    Send-Keys 'demo1234'
    Invoke-El (Find-El $login -Name 'Sign in' -ControlType Button)
    $main = Wait-Window $pidApp 'Swifttill*' 30
    Set-WindowFrame $main
    Start-Sleep 2

    # ── Start recording ──────────────────────────────────────────────────────
    $psi = New-Object Diagnostics.ProcessStartInfo $ffmpeg.FullName
    $psi.Arguments = "-hide_banner -loglevel error -y -f gdigrab -framerate 30 -draw_mouse 0 " +
                     "-offset_x 0 -offset_y 0 -video_size 1920x1080 -i desktop " +
                     "-c:v libx264 -preset ultrafast -crf 14 -pix_fmt yuv420p `"$raw`""
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $rec = [Diagnostics.Process]::Start($psi)
    $clock.Start()
    Start-Sleep 2

    # ── Scene: billing — scanning a basket, cash, change ─────────────────────
    $sale = Open-Screen '*Sales' '?? Sale' 'Sale -*'
    $edits = $sale.FindAll($script:TS::Descendants,
        (New-Object $script:Cond ($script:AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
    $scan = $edits[4]
    $scan.SetFocus()
    Mark-Start 'billing'
    Start-Sleep -Milliseconds 800
    foreach ($code in '2001000000173', '2001000000203', '2001000000258', '2001000000265',
                      '2001000000265', '2001000000012', '2001000000142') {
        Type-Slow $code 28
        Send-Keys '{ENTER}'
        Start-Sleep -Milliseconds 650
    }
    $edits[8].SetFocus()
    Start-Sleep -Milliseconds 400
    Type-Slow '4000' 160
    Start-Sleep -Milliseconds 2200
    Mark-End 'billing'
    Invoke-El (Find-El $sale -Name 'Close' -ControlType Button)

    # ── Scene: receipt — shop identity then the live receipt preview ─────────
    $biz = Open-Screen '*Admin' '*Business Settings' 'Business Settings*'
    Mark-Start 'receipt'
    Start-Sleep -Milliseconds 1500
    Invoke-El (Find-El $biz -Name '*Preview*' -ControlType TabItem)
    Start-Sleep -Milliseconds 3000
    Mark-End 'receipt'
    Close-Window $biz

    # ── Scene: overview — top sellers + low stock ────────────────────────────
    Wait-Until { @(Get-AppWindows $pidApp).Count -eq 1 } 15 'settings to close' | Out-Null
    [void][Win32]::SetForegroundWindow((Get-Hwnd $main))
    Start-Sleep -Milliseconds 800
    Mark-Start 'inventory'
    Start-Sleep -Milliseconds 3500
    Mark-End 'inventory'

    # ── Scene: sales report — today → this week → this month, open an invoice ──
    $rep = Open-Screen '*Reports' '*Sales Report' 'Sales Report*'
    Mark-Start 'report'
    Start-Sleep -Milliseconds 1000
    foreach ($b in 'This week', 'This month') {
        Invoke-El (Find-El $rep -Name $b -ControlType Button)
        Start-Sleep -Milliseconds 1600
    }
    Invoke-El (Find-El $rep -Name 'View' -ControlType Button)
    Start-Sleep -Milliseconds 700
    $pane = $rep.FindAll($script:TS::Descendants,
        (New-Object $script:Cond ($script:AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Pane))) |
        Where-Object { $_.Current.BoundingRectangle.X -gt 1400 } | Select-Object -First 1
    if ($pane) {
        $sp = $pane.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
        foreach ($pct in 25, 50, 75, 100) { $sp.SetScrollPercent(-1, $pct); Start-Sleep -Milliseconds 120 }
    }
    Start-Sleep -Milliseconds 2500
    Mark-End 'report'
    Close-Window $rep

    # ── Scene: khata — pick customers, see balances and payments ─────────────
    $led = Open-Screen '*Finance' '*Khata' 'Customer Ledger*'
    $rows = @($led.FindAll($script:TS::Descendants,
        (New-Object $script:Cond ($script:AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::DataItem))))
    Mark-Start 'khata'
    Start-Sleep -Milliseconds 800
    Invoke-El $rows[1]; Start-Sleep -Milliseconds 300
    Invoke-El (Find-El $led -Name 'All' -ControlType Button); Start-Sleep -Milliseconds 1500
    Invoke-El $rows[0]; Start-Sleep -Milliseconds 300
    Invoke-El (Find-El $led -Name 'All' -ControlType Button); Start-Sleep -Milliseconds 2600
    Mark-End 'khata'
    Close-Window $led
    Start-Sleep 1
}
finally {
    if ($rec -and -not $rec.HasExited) {
        $rec.StandardInput.Write('q'); $rec.StandardInput.Flush()
        if (-not $rec.WaitForExit(20000)) { $rec.Kill() }
    }
    if (-not $proc.HasExited) { $proc.CloseMainWindow() | Out-Null; Start-Sleep 2; if (-not $proc.HasExited) { $proc.Kill() } }
}
$scenes | ConvertTo-Json -Depth 3 | Set-Content -Encoding utf8 (Join-Path $outDir 'scenes.json')
Write-Host "Recorded $raw"
$scenes | ConvertTo-Json -Depth 3
