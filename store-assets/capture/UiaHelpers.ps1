# Helpers for driving SwiftTill through Windows UI Automation and grabbing exact-size window
# captures. Uses only what ships with Windows (UIAutomationClient + user32/dwmapi) — no installs.
# Dot-source it:  . "$PSScriptRoot\UiaHelpers.ps1"

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Win32 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, string l);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
    public static void Click(int x, int y) { SetCursorPos(x, y); mouse_event(2, 0, 0, 0, IntPtr.Zero); mouse_event(4, 0, 0, 0, IntPtr.Zero); }
    public static RECT Visible(IntPtr h) { RECT r; DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))); return r; }
}
'@
# Per-monitor-v2 DPI awareness: every coordinate below is in physical pixels.
[void][Win32]::SetProcessDpiAwarenessContext([IntPtr]-4)

$script:AE   = [System.Windows.Automation.AutomationElement]
$script:TS   = [System.Windows.Automation.TreeScope]
$script:Cond = [System.Windows.Automation.PropertyCondition]

function Wait-Until([scriptblock]$Test, [int]$TimeoutSec = 20, [string]$What = 'condition') {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $TimeoutSec) {
        $r = & $Test
        if ($r) { return $r }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for $What"
}

# All windows of the process, including owned dialogs (UIA nests those under their owner).
function Get-AppWindows([int]$ProcessId) {
    $c = New-Object System.Windows.Automation.AndCondition @(
        (New-Object $script:Cond ($script:AE::ProcessIdProperty, $ProcessId)),
        (New-Object $script:Cond ($script:AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)))
    $script:AE::RootElement.FindAll($script:TS::Descendants, $c)
}

function Get-TopWindow([int]$ProcessId, [string]$TitleLike = '*') {
    Get-AppWindows $ProcessId | Where-Object { $_.Current.Name -like $TitleLike } | Select-Object -First 1
}

function Close-Window($Window) {
    $p = $null
    if ($Window.TryGetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern, [ref]$p)) { $p.Close() }
    Start-Sleep -Milliseconds 800
}

function Wait-Window([int]$ProcessId, [string]$TitleLike, [int]$TimeoutSec = 30) {
    Wait-Until { Get-TopWindow $ProcessId $TitleLike } $TimeoutSec "window '$TitleLike'"
}

function Find-El($Root, [string]$Name = $null, [string]$AutomationId = $null, [string]$ControlType = $null) {
    $all = $Root.FindAll($script:TS::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) {
        $cur = $e.Current
        if ($Name -and $cur.Name -notlike $Name) { continue }
        if ($AutomationId -and $cur.AutomationId -ne $AutomationId) { continue }
        if ($ControlType -and $cur.ControlType.ProgrammaticName -ne "ControlType.$ControlType") { continue }
        return $e
    }
    $null
}

function Invoke-El($El) {
    $p = $null
    if ($El.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$p)) { $p.Invoke(); return }
    if ($El.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$p)) { $p.Expand(); return }
    if ($El.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$p)) { $p.Select(); return }
    throw "Element '$($El.Current.Name)' cannot be invoked"
}

function Set-Text($El, [string]$Text) {
    $El.SetFocus()
    $p = $null
    if ($El.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$p)) { $p.SetValue($Text); return }
    [System.Windows.Forms.SendKeys]::SendWait($Text)
}

function Send-Keys([string]$Keys) { [System.Windows.Forms.SendKeys]::SendWait($Keys); Start-Sleep -Milliseconds 150 }

function Get-Hwnd($Window) { [IntPtr]$Window.Current.NativeWindowHandle }

# Resize a window so its VISIBLE frame (DWM extended frame, excluding the invisible resize
# border) is exactly $Width x $Height physical pixels, at the top-left of the primary screen.
function Set-WindowFrame($Window, [int]$Width = 1920, [int]$Height = 1080) {
    $h = Get-Hwnd $Window
    [void][Win32]::ShowWindow($h, 9)   # SW_RESTORE (un-maximise)
    Start-Sleep -Milliseconds 300
    for ($i = 0; $i -lt 3; $i++) {
        $out = New-Object Win32+RECT; [void][Win32]::GetWindowRect($h, [ref]$out)
        $vis = [Win32]::Visible($h)
        $padL = $vis.L - $out.L; $padT = $vis.T - $out.T; $padR = $out.R - $vis.R; $padB = $out.B - $vis.B
        [void][Win32]::SetWindowPos($h, [IntPtr]::Zero, -$padL, -$padT, $Width + $padL + $padR, $Height + $padT + $padB, 0x0040)
        Start-Sleep -Milliseconds 400
        $vis = [Win32]::Visible($h)
        if (($vis.R - $vis.L) -eq $Width -and ($vis.B - $vis.T) -eq $Height) { break }
    }
    [void][Win32]::SetForegroundWindow($h)
}

# Screen-grabs the window's visible frame (must be on top and unobscured) to a PNG.
function Save-WindowShot($Window, [string]$Path) {
    $h = Get-Hwnd $Window
    [void][Win32]::SetForegroundWindow($h)
    Start-Sleep -Milliseconds 700      # let animations / focus rings settle
    $r = [Win32]::Visible($h)
    $w = $r.R - $r.L; $hgt = $r.B - $r.T
    $bmp = New-Object System.Drawing.Bitmap $w, $hgt
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.L, $r.T, 0, 0, (New-Object System.Drawing.Size $w, $hgt))
    $g.Dispose()
    New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ("saved {0} ({1}x{2})" -f (Split-Path $Path -Leaf), $w, $hgt)
}

# Dumps the automation tree (name / id / type) to help write or repair capture steps.
function Show-Tree($Root, [int]$Max = 400) {
    $Root.FindAll($script:TS::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
        Select-Object -First $Max |
        ForEach-Object { '{0,-28} {1,-30} {2}' -f $_.Current.ControlType.ProgrammaticName.Replace('ControlType.', ''), $_.Current.AutomationId, $_.Current.Name }
}

# Opens a top-level menu (matched by wildcard, e.g. '*Sales') and invokes one of its items
# (e.g. '*Sale Return'). Menu labels start with emoji, so match on the words after them.
function Invoke-MenuItem([int]$ProcessId, $Window, [string]$TopLike, [string]$ItemLike) {
    $c = New-Object $script:Cond ($script:AE::ProcessIdProperty, $ProcessId)
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        [void][Win32]::SetForegroundWindow((Get-Hwnd $Window))
        $top = Find-El $Window -Name $TopLike -ControlType MenuItem
        if (-not $top) { throw "Menu '$TopLike' not found" }
        $ec = $top.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
        try { $ec.Expand() } catch { }
        $item = $null
        for ($i = 0; $i -lt 8 -and -not $item; $i++) {
            Start-Sleep -Milliseconds 400
            $item = $script:AE::RootElement.FindAll($script:TS::Descendants, $c) |
                Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.MenuItem' -and
                               $_.Current.Name -like $ItemLike -and $_.Current.Name -notlike $TopLike -and
                               -not $_.Current.IsOffscreen } |
                Select-Object -First 1
        }
        if ($item) { Invoke-El $item; return }
        try { $ec.Collapse() } catch { }
        Start-Sleep -Milliseconds 800
    }
    throw "Menu item '$ItemLike' not found under '$TopLike'"
}

# Common file/folder dialogs (#32770) expose their fields only as native panes, so drive them
# with window messages: put a path in the "Folder:"/"File name:" box (control 1152, an Edit
# inside a ComboBoxEx) and click the default button (control 1).
function Select-DialogPath($Dialog, [string]$Path) {
    $box = Find-El $Dialog -AutomationId '1152'
    $h = [IntPtr]$box.Current.NativeWindowHandle
    $edit = [Win32]::FindWindowEx($h, [IntPtr]::Zero, 'ComboBox', $null)
    if ($edit -ne [IntPtr]::Zero) { $edit = [Win32]::FindWindowEx($edit, [IntPtr]::Zero, 'Edit', $null) }
    else { $edit = [Win32]::FindWindowEx($h, [IntPtr]::Zero, 'Edit', $null) }
    if ($edit -eq [IntPtr]::Zero) { $edit = $h }
    [void][Win32]::SendMessage($edit, 0x000C, [IntPtr]::Zero, $Path)          # WM_SETTEXT
    Start-Sleep -Milliseconds 300
    $hDlg = [IntPtr]$Dialog.Current.NativeWindowHandle
    $pidDlg = $Dialog.Current.ProcessId
    [void][Win32]::SetForegroundWindow($hDlg)
    Click-El (Find-El $Dialog -AutomationId '1')                               # "Select Folder" / "Open"
    $open = { @(Get-AppWindows $pidDlg | Where-Object { $_.Current.NativeWindowHandle -eq [int]$hDlg }).Count -gt 0 }
    for ($i = 0; $i -lt 12 -and (& $open); $i++) {
        Start-Sleep -Milliseconds 500
        if ($i -eq 5) { [void][Win32]::SetForegroundWindow($hDlg); Send-Keys '{ENTER}' }
    }
    if (& $open) { throw 'Folder dialog did not accept the path' }
}

# Real left-click in the centre of an element (for native controls that ignore UIA/messages).
function Click-El($El) {
    $r = $El.Current.BoundingRectangle
    [Win32]::Click([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
    Start-Sleep -Milliseconds 400
}
