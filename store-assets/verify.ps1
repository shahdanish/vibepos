<#
.SYNOPSIS
    Checks every Store asset PNG: real PNG signature, exact pixel size, file-size limit.
    Prints a pass/fail table; exit code 1 if anything fails.

    powershell -ExecutionPolicy Bypass -File store-assets\verify.ps1
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = $PSScriptRoot
$MB = 1MB

# Path, width, height, max bytes, Partner Center field
$expected = @(
    ,@('logos/poster-9x16-1440x2160.png', 1440, 2160, 50MB, '9:16 Poster art (1440x2160)')
    ,@('logos/poster-9x16-720x1080.png',   720, 1080, 50MB, '9:16 Poster art (720x1080)')
    ,@('logos/boxart-1x1-2160x2160.png',  2160, 2160, 50MB, '1:1 Box art (2160x2160)')
    ,@('logos/boxart-1x1-1080x1080.png',  1080, 1080, 50MB, '1:1 Box art (1080x1080)')
    ,@('logos/tile-300.png',               300,  300,  5MB, '1:1 App tile icon (300x300)')
    ,@('logos/tile-150.png',               150,  150,  5MB, '1:1 App tile icon (150x150)')
    ,@('logos/tile-71.png',                 71,   71,  5MB, '1:1 App tile icon (71x71)')
    ,@('hero/superhero-16x9-3840x2160.png',3840, 2160, 50MB, '16:9 Super hero art (3840x2160)')
    ,@('hero/superhero-16x9-1920x1080.png',1920, 1080, 50MB, '16:9 Super hero art (1920x1080)')
    ,@('trailer/trailer-thumbnail-1920x1080.png', 1920, 1080, 2MB, 'Trailer thumbnail')
)
foreach ($f in Get-ChildItem (Join-Path $root 'screenshots') -Filter *.png) {
    $expected += ,@("screenshots/$($f.Name)", 1920, 1080, 50MB, 'Desktop screenshot')
}
foreach ($f in Get-ChildItem (Join-Path $root 'screenshots\framed') -Filter *.png -ErrorAction SilentlyContinue) {
    $expected += ,@("screenshots/framed/$($f.Name)", 2560, 1440, 50MB, 'Desktop screenshot (framed alt.)')
}

$sig = [byte[]](0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
$rows = foreach ($e in $expected) {
    $path = Join-Path $root $e[0]
    $row = [ordered]@{ File = $e[0]; Expected = "$($e[1])x$($e[2])"; Actual = '-'; SizeMB = '-'; Limit = "{0} MB" -f ($e[3] / $MB); PNG = '-'; Result = 'FAIL' }
    if (Test-Path $path) {
        $bytes = [IO.File]::ReadAllBytes($path)
        $isPng = $bytes.Length -ge 8 -and -not (Compare-Object $sig $bytes[0..7])
        $img = [System.Drawing.Image]::FromFile($path)
        $row.Actual = "$($img.Width)x$($img.Height)"
        $img.Dispose()
        $row.SizeMB = '{0:N2}' -f ($bytes.Length / $MB)
        $row.PNG = if ($isPng) { 'yes' } else { 'no' }
        if ($isPng -and $row.Actual -eq $row.Expected -and $bytes.Length -lt $e[3]) { $row.Result = 'PASS' }
    } else { $row.Actual = 'missing' }
    [pscustomobject]$row
}
$rows | Format-Table -AutoSize | Out-String -Width 200 | Write-Output
$failed = @($rows | Where-Object Result -ne 'PASS').Count
Write-Output ("{0} checked, {1} passed, {2} failed" -f $rows.Count, ($rows.Count - $failed), $failed)

# Trailer: container/size only (codec details are printed by src/trailer.py and ffmpeg -i)
$mp4 = Join-Path $root 'trailer\swifttill-trailer-1080p.mp4'
if (Test-Path $mp4) {
    $len = (Get-Item $mp4).Length
    $ok = $len -lt 2GB
    Write-Output ("trailer/swifttill-trailer-1080p.mp4  {0:N2} MB  limit 2 GB  {1}" -f ($len / 1MB), $(if ($ok) { 'PASS' } else { 'FAIL' }))
    if (-not $ok) { $failed++ }
}
if ($failed) { exit 1 }
