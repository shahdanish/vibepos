param([string]$RawScreenshots = '')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot 'marketing'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$assets = Split-Path $PSScriptRoot -Parent
$logo = [Drawing.Image]::FromFile((Join-Path $assets 'logos/tile-300.png'))
$ink = [Drawing.ColorTranslator]::FromHtml('#15273B')
$green = [Drawing.ColorTranslator]::FromHtml('#267C4A')
$muted = [Drawing.ColorTranslator]::FromHtml('#52645F')
$paper = [Drawing.ColorTranslator]::FromHtml('#F6F3EB')
function Text($g, [string]$value, [float]$size, [float]$x, [float]$y, $color, [bool]$bold = $false) {
    $style = if ($bold) { [Drawing.FontStyle]::Bold } else { [Drawing.FontStyle]::Regular }
    $font = [Drawing.Font]::new('Segoe UI', $size, $style, [Drawing.GraphicsUnit]::Pixel)
    $brush = [Drawing.SolidBrush]::new($color)
    $g.DrawString($value, $font, $brush, $x, $y)
    $font.Dispose(); $brush.Dispose()
}
function Make-Card([string]$name, [int]$w, [int]$h, [string]$headline, [string]$subline) {
    $bitmap = [Drawing.Bitmap]::new($w, $h)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear($paper)
    $scale = $w / 1080.0
    $margin = 76 * $scale
    $brandSize = 35 * $scale
    $g.DrawImage($logo, [float]$margin, [float](54*$scale), [float](72*$scale), [float](72*$scale))
    Text $g 'SwiftTill' $brandSize ($margin+90*$scale) (64*$scale) $ink $true
    $compact = $h -lt $w * 0.7
    $headlineY = if ($compact) { 170*$scale } else { 265*$scale }
    $headlineSize = if ($compact) { 68*$scale } else { 83*$scale }
    Text $g $headline $headlineSize $margin $headlineY $ink $true
    $subY = if ($compact) { $headlineY + 100*$scale } else { $headlineY + 224*$scale }
    Text $g $subline (27*$scale) $margin $subY $muted
    $lineBrush = [Drawing.SolidBrush]::new($green)
    $g.FillRectangle($lineBrush, [float]$margin, [float]($h-158*$scale), [float](330*$scale), [float](63*$scale))
    Text $g 'Get it on Microsoft Store' (22*$scale) ($margin+18*$scale) ($h-145*$scale) ([Drawing.Color]::White) $true
    Text $g 'Free Lite  |  Optional Pro subscriptions' (19*$scale) $margin ($h-68*$scale) $muted
    $lineBrush.Dispose()
    $bitmap.Save((Join-Path $out "$name.png"), [Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bitmap.Dispose()
}
Make-Card 'social-square-1080' 1080 1080 "Your shop.`nOne clear view." "Offline POS for Windows.`nBilling. Stock. Receipts. Customer credit."
Make-Card 'social-banner-1200x630' 1200 630 'A calmer counter starts here.' 'Offline billing, inventory and customer credit for Windows.'
Make-Card 'video-thumbnail-1920x1080' 1920 1080 'See SwiftTill in action.' 'A real retail workflow. Sample shop data.'
$logo.Dispose()
Write-Output "Marketing PNGs written to $out"
