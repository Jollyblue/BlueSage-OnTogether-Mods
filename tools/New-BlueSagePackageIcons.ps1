#Requires -Version 7.0
<#
.SYNOPSIS
    Builds the square 256x256 Thunderstore/Gale icons (plus 1024x1024 masters) for every BlueSage package.

.DESCRIPTION
    Blue's rule (2026-10-18): the in-game screenshot is never altered. It is scaled as a whole, keeping its
    proportions (no crop, no recolor), and placed in a square frame. Only borders, side panels, and text
    overlays are added on top.

    Sources live in art/source and are pinned by SHA-256 in art/source/SOURCES.json, so a changed or swapped
    screenshot fails loudly instead of quietly producing a different icon.

    Idempotent: running it again with the same sources rewrites byte-identical files.

.PARAMETER Root
    ModSuite root. Defaults to the folder above this script.

.PARAMETER Only
    Optional package folder names to build; default builds all four.
#>
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string[]]$Only = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Write-Log([string]$Message) {
    Write-Host ('{0} {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message)
}

function Get-Color([string]$Hex, [int]$Alpha = 255) {
    $h = $Hex.TrimStart('#')
    [System.Drawing.Color]::FromArgb($Alpha, [Convert]::ToInt32($h.Substring(0, 2), 16), [Convert]::ToInt32($h.Substring(2, 2), 16), [Convert]::ToInt32($h.Substring(4, 2), 16))
}

function New-RoundedPath([float]$X, [float]$Y, [float]$W, [float]$H, [float]$R) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $R * 2
    $p.AddArc($X, $Y, $d, $d, 180, 90)
    $p.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
    $p.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
    $p.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

# Fits text to a box, then draws it centered with a dark outline and a soft shadow so it reads at 256 px.
function Write-OutlinedText($G, [string]$Text, [string]$FontName, [float]$MaxSize, [float]$CenterX, [float]$Top, [float]$MaxWidth, [float]$MaxHeight, [System.Drawing.Color]$Fill, [System.Drawing.Color]$Outline, [float]$OutlineWidth) {
    $family = [System.Drawing.FontFamily]::new($FontName)
    $format = [System.Drawing.StringFormat]::GenericTypographic
    $size = $MaxSize
    while ($true) {
        $probe = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $probe.AddString($Text, $family, [int][System.Drawing.FontStyle]::Regular, $size, [System.Drawing.PointF]::new(0, 0), $format)
        $b = $probe.GetBounds()
        $probe.Dispose()
        if (($b.Width -le $MaxWidth -and $b.Height -le $MaxHeight) -or $size -le 12) { break }
        $size -= 2
    }
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddString($Text, $family, [int][System.Drawing.FontStyle]::Regular, $size, [System.Drawing.PointF]::new(0, 0), $format)
    $b = $path.GetBounds()
    $m = [System.Drawing.Drawing2D.Matrix]::new()
    $m.Translate($CenterX - $b.Width / 2 - $b.X, $Top + ($MaxHeight - $b.Height) / 2 - $b.Y)
    $path.Transform($m)

    $shadow = $path.Clone()
    $sm = [System.Drawing.Drawing2D.Matrix]::new()
    $sm.Translate($OutlineWidth * 0.45, $OutlineWidth * 0.55)
    $shadow.Transform($sm)
    $shadowPen = [System.Drawing.Pen]::new((Get-Color '0A0F1E' 150), $OutlineWidth * 1.15)
    $shadowPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $G.DrawPath($shadowPen, $shadow)

    $pen = [System.Drawing.Pen]::new($Outline, $OutlineWidth)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $G.DrawPath($pen, $path)
    # Lit fill: a lighter top fading into the base color, then a thin white rim for a premium edge.
    $fb = $path.GetBounds()
    $lit = [System.Drawing.Color]::FromArgb(255, [Math]::Min(255, $Fill.R + 70), [Math]::Min(255, $Fill.G + 70), [Math]::Min(255, $Fill.B + 70))
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.RectangleF]::new($fb.X, $fb.Y - 1, $fb.Width, $fb.Height + 2), $lit, $Fill, 90.0)
    $G.FillPath($brush, $path)
    $rim = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(90, 255, 255, 255), [Math]::Max(1.0, $OutlineWidth * 0.12))
    $G.DrawPath($rim, $path)
    foreach ($d in $shadowPen, $pen, $brush, $rim, $path, $shadow, $family) { $d.Dispose() }
}

# Four-point sparkle, used for the side-panel pattern and the band accents.
function Add-Sparkle($G, [float]$Cx, [float]$Cy, [float]$R, [System.Drawing.Color]$Color) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $k = $R * 0.28
    $p.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($Cx, $Cy - $R), [System.Drawing.PointF]::new($Cx + $k, $Cy - $k),
        [System.Drawing.PointF]::new($Cx + $R, $Cy), [System.Drawing.PointF]::new($Cx + $k, $Cy + $k),
        [System.Drawing.PointF]::new($Cx, $Cy + $R), [System.Drawing.PointF]::new($Cx - $k, $Cy + $k),
        [System.Drawing.PointF]::new($Cx - $R, $Cy), [System.Drawing.PointF]::new($Cx - $k, $Cy - $k)))
    $b = [System.Drawing.SolidBrush]::new($Color)
    $G.FillPath($b, $p)
    $b.Dispose(); $p.Dispose()
}

function Add-Heart($G, [float]$Cx, [float]$Cy, [float]$Size, [System.Drawing.Color]$Fill, [System.Drawing.Color]$Edge, [float]$EdgeWidth) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $s = $Size
    $p.AddBezier($Cx, $Cy + $s * 0.45, $Cx - $s * 0.95, $Cy - $s * 0.15, $Cx - $s * 0.45, $Cy - $s * 0.75, $Cx, $Cy - $s * 0.28)
    $p.AddBezier($Cx, $Cy - $s * 0.28, $Cx + $s * 0.45, $Cy - $s * 0.75, $Cx + $s * 0.95, $Cy - $s * 0.15, $Cx, $Cy + $s * 0.45)
    $p.CloseFigure()
    $pen = [System.Drawing.Pen]::new($Edge, $EdgeWidth)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $G.DrawPath($pen, $p)
    $b = [System.Drawing.SolidBrush]::new($Fill)
    $G.FillPath($b, $p)
    $pen.Dispose(); $b.Dispose(); $p.Dispose()
}

function Get-Shade([System.Drawing.Color]$C, [float]$F) {
    if ($F -ge 0) { return [System.Drawing.Color]::FromArgb($C.A, [int]($C.R + (255 - $C.R) * $F), [int]($C.G + (255 - $C.G) * $F), [int]($C.B + (255 - $C.B) * $F)) }
    $m = 1 + $F
    [System.Drawing.Color]::FromArgb($C.A, [int]($C.R * $m), [int]($C.G * $m), [int]($C.B * $m))
}

function New-PackageIcon([hashtable]$Spec, [string]$SourcePath, [string]$MasterPath, [string]$IconPath) {
    $S = 1024
    $border = 28
    $canvas = [System.Drawing.Bitmap]::new($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($canvas)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $accent = Get-Color $Spec.Accent
    $cream = Get-Color 'F3E9D7'

    # Square bevel frame: dark outer rim, lit accent band (light top-left to deep bottom-right), cream hairline.
    $g.Clear((Get-Color '0A0F1E'))
    $bandRect = [System.Drawing.Rectangle]::new(5, 5, $S - 10, $S - 10)
    $bevel = [System.Drawing.Drawing2D.LinearGradientBrush]::new($bandRect, (Get-Shade $accent 0.35), (Get-Shade $accent -0.35), 45.0)
    $g.FillRectangle($bevel, $bandRect)
    $bevel.Dispose()
    $hi = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(120, 255, 255, 255), 3)
    $g.DrawLine($hi, 8, 8, $S - 9, 8); $g.DrawLine($hi, 8, 8, 8, $S - 9)
    $hi.Dispose()

    $field = [System.Drawing.Rectangle]::new($border, $border, $S - 2 * $border, $S - 2 * $border)
    $grad = [System.Drawing.Drawing2D.LinearGradientBrush]::new($field, (Get-Color '22355C'), (Get-Color '0C1322'), 70.0)
    $g.FillRectangle($grad, $field)
    $grad.Dispose()

    # Side-panel atmosphere: an accent glow from the center, then a deterministic sparkle field (fixed seed,
    # so reruns are byte-identical).
    $glowPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $glowPath.AddEllipse(-$S * 0.25, -$S * 0.1, $S * 1.5, $S * 1.2)
    $glow = [System.Drawing.Drawing2D.PathGradientBrush]::new($glowPath)
    $glow.CenterColor = [System.Drawing.Color]::FromArgb(70, $accent.R, $accent.G, $accent.B)
    $glow.SurroundColors = [System.Drawing.Color[]]@([System.Drawing.Color]::FromArgb(0, $accent.R, $accent.G, $accent.B))
    $g.SetClip($field)
    $g.FillRectangle($glow, $field)
    $rng = [System.Random]::new(20261018)
    for ($i = 0; $i -lt 70; $i++) {
        $sx = $border + $rng.NextDouble() * ($S - 2 * $border)
        $sy = $border + $rng.NextDouble() * ($S - 2 * $border)
        $sr = 3 + $rng.NextDouble() * 9
        $sa = 40 + $rng.Next(0, 110)
        Add-Sparkle $g $sx $sy $sr ([System.Drawing.Color]::FromArgb($sa, 243, 233, 215))
    }
    $g.ResetClip()
    $glow.Dispose(); $glowPath.Dispose()

    # Inner shadow just inside the frame so the field sits recessed.
    for ($i = 0; $i -lt 10; $i++) {
        $isp = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(26 - 2 * $i, 0, 0, 0), 2)
        $g.DrawRectangle($isp, $border + $i, $border + $i, $S - 2 * $border - 2 * $i - 1, $S - 2 * $border - 2 * $i - 1)
        $isp.Dispose()
    }
    $hair = [System.Drawing.Pen]::new($cream, 3)
    $g.DrawRectangle($hair, $border - 2, $border - 2, $S - 2 * $border + 3, $S - 2 * $border + 3)
    $hair.Dispose()

    # The screenshot: scaled whole to the field height, proportions kept, centered. Never cropped or recolored.
    $shot = [System.Drawing.Image]::FromFile($SourcePath)
    $h = $S - 2 * $border
    $w = [int][Math]::Round($shot.Width * $h / $shot.Height)
    if ($w -gt $h) { $w = $h; $h = [int][Math]::Round($shot.Height * $w / $shot.Width) }
    $x = [int](($S - $w) / 2)
    $y = [int](($S - $h) / 2)
    # Drop shadow under the photo mat, stepped so it reads as soft at 256 px.
    for ($i = 12; $i -ge 1; $i--) {
        $shadeBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(14, 0, 0, 0))
        $g.FillRectangle($shadeBrush, $x - 8 + $i, $y - 8 + $i * 1.4, $w + 16, $h + 16)
        $shadeBrush.Dispose()
    }
    $matBrush = [System.Drawing.SolidBrush]::new($cream)
    $g.FillRectangle($matBrush, $x - 8, $y - 8, $w + 16, $h + 16)
    $matBrush.Dispose()
    $attrs = [System.Drawing.Imaging.ImageAttributes]::new()
    $attrs.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $g.DrawImage($shot, [System.Drawing.Rectangle]::new($x, $y, $w, $h), 0, 0, $shot.Width, $shot.Height, [System.Drawing.GraphicsUnit]::Pixel, $attrs)
    $attrs.Dispose()
    $shot.Dispose()
    $matLine = [System.Drawing.Pen]::new((Get-Shade $accent -0.2), 2)
    $g.DrawRectangle($matLine, $x - 1, $y - 1, $w + 1, $h + 1)
    $matLine.Dispose()

    # Legibility plate: a soft dark fade over the lower part, where the screenshot shows desk legs and water.
    $plateTop = 548
    $plate = [System.Drawing.Rectangle]::new($border, $plateTop, $S - 2 * $border, $S - $border - $plateTop)
    $fade = [System.Drawing.Drawing2D.LinearGradientBrush]::new($plate, (Get-Color '0B1222' 0), (Get-Color '0B1222' 215), 90.0)
    $blend = [System.Drawing.Drawing2D.Blend]::new(3)
    $blend.Positions = [float[]](0.0, 0.35, 1.0)
    $blend.Factors = [float[]](0.0, 0.75, 1.0)
    $fade.Blend = $blend
    $g.FillRectangle($fade, $plate)
    $fade.Dispose()

    $ink = Get-Color '101A33'
    $inner = $S - 2 * $border - 56
    Write-OutlinedText $g 'BlueSage' 'Segoe UI Black' 190 ($S / 2) 588 $inner 170 (Get-Color '62B8FF') $ink 22
    Write-OutlinedText $g $Spec.Title 'Segoe UI Black' 130 ($S / 2) 752 $inner 118 (Get-Color $Spec.TitleColor) $ink 18

    # Band pill with the package's key promise.
    $bandW = 820; $bandH = 74; $bandX = ($S - $bandW) / 2; $bandY = 880
    $bandBase = Get-Color $Spec.BandColor
    $shadowPill = New-RoundedPath ($bandX + 4) ($bandY + 7) $bandW $bandH 37
    $shadowPillBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(140, 5, 8, 18))
    $g.FillPath($shadowPillBrush, $shadowPill)
    $pill = New-RoundedPath $bandX $bandY $bandW $bandH 37
    $pillBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.RectangleF]::new($bandX, $bandY - 1, $bandW, $bandH + 2), (Get-Shade $bandBase 0.25), (Get-Shade $bandBase -0.25), 90.0)
    $g.FillPath($pillBrush, $pill)
    # Gloss: a soft white sheen over the top half of the pill.
    $gloss = New-RoundedPath ($bandX + 10) ($bandY + 5) ($bandW - 20) ($bandH / 2 - 4) 26
    $glossBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.RectangleF]::new($bandX, $bandY + 4, $bandW, $bandH / 2), [System.Drawing.Color]::FromArgb(85, 255, 255, 255), [System.Drawing.Color]::FromArgb(0, 255, 255, 255), 90.0)
    $g.FillPath($glossBrush, $gloss)
    $pillPen = [System.Drawing.Pen]::new($cream, 5)
    $g.DrawPath($pillPen, $pill)
    foreach ($d in $shadowPill, $shadowPillBrush, $pill, $pillBrush, $gloss, $glossBrush, $pillPen) { $d.Dispose() }
    Add-Sparkle $g ($bandX + 40) ($bandY + $bandH / 2) 15 $cream
    Add-Sparkle $g ($bandX + $bandW - 40) ($bandY + $bandH / 2) 15 $cream
    Write-OutlinedText $g $Spec.Band 'Segoe UI Black' 54 ($S / 2) ($bandY + 10) ($bandW - 130) ($bandH - 20) (Get-Color 'FFFFFF') (Get-Shade $bandBase -0.45) 5

    # Corner studs and a heart medallion on the top rail: the BlueSage signature on every icon.
    $near = $border / 2 + 2
    $far = $S - $near
    foreach ($c in @(@($near, $near), @($far, $near), @($near, $far), @($far, $far))) {
        Add-Sparkle $g $c[0] $c[1] 17 (Get-Color '0A0F1E')
        Add-Sparkle $g $c[0] $c[1] 12 $cream
    }
    $medal = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $medal.AddEllipse($S / 2 - 34, -4, 68, 68)
    $medalBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.RectangleF]::new($S / 2 - 34, -4, 68, 68), (Get-Shade $accent 0.3), (Get-Shade $accent -0.3), 90.0)
    $g.FillPath($medalBrush, $medal)
    $medalPen = [System.Drawing.Pen]::new($cream, 4)
    $g.DrawPath($medalPen, $medal)
    $medalRim = [System.Drawing.Pen]::new((Get-Color '0A0F1E'), 2)
    $g.DrawEllipse($medalRim, $S / 2 - 37, -7, 74, 74)
    foreach ($d in $medal, $medalBrush, $medalPen, $medalRim) { $d.Dispose() }
    Add-Heart $g ($S / 2) 32 34 (Get-Color 'FF6F91') (Get-Color '5A1830') 4
    Write-OutlinedText $g $Spec.Footer 'Segoe UI Black' 44 ($S / 2) 958 ($S - 2 * $border - 40) 40 (Get-Color 'F3E9D7') $ink 8

    $g.Dispose()
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $MasterPath) | Out-Null
    $canvas.Save($MasterPath, [System.Drawing.Imaging.ImageFormat]::Png)

    $small = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $sg = [System.Drawing.Graphics]::FromImage($small)
    $sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $sg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $sg.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $sg.DrawImage($canvas, 0, 0, 256, 256)
    $sg.Dispose()
    $small.Save($IconPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $small.Dispose()
    $canvas.Dispose()
}

function Get-PackageVersion([string]$ProjectDir, [string]$Fallback) {
    $manifest = Join-Path $ProjectDir 'manifest.json'
    if (Test-Path -LiteralPath $manifest) { return (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).version_number }
    return $Fallback
}

$sourceDir = Join-Path $Root 'art\source'
$pins = Get-Content -LiteralPath (Join-Path $sourceDir 'SOURCES.json') -Raw | ConvertFrom-Json
foreach ($pin in $pins.sources) {
    $path = Join-Path $sourceDir $pin.file
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($actual -ne $pin.sha256) { throw "Source screenshot changed: $($pin.file) is $actual, pinned $($pin.sha256). Re-pin only on Blue's say-so." }
}
Write-Log "Sources match their pins ($(@($pins.sources).Count) screenshots)."

$packages = @(
    # Blue, 2026-10-23: the public band carries no version until an official 1.0.0 (the 0.2.5 band had gone stale).
    @{ Folder = 'BlueSage_QoL_Tweaks_Beta'; Source = 'BlueQoL.png'; Title = 'QoL Tweaks'; TitleColor = '9BE08A'; Accent = '7FC8A9'; BandColor = '6B4FBF'; Band = 'EARLY ACCESS - BETA'; Footer = 'discord.gg/JujMEwtN3q'; Fallback = '0.2.7' },
    @{ Folder = 'BlueSagePatched_PlayerLimitLift'; Source = 'BlueMods.png'; Title = 'PlayerLimitLift'; TitleColor = '9BE08A'; Accent = '7FC8A9'; BandColor = 'D2554F'; Band = '128 JOINABLE + HOST'; Footer = 'ONLY HOST INSTALL NEEDED'; Fallback = '1.1.8' },
)

foreach ($spec in $packages) {
    if ($Only.Count -gt 0 -and $Only -notcontains $spec.Folder) { continue }
    $projectDir = Join-Path $Root $spec.Folder
    if (-not (Test-Path -LiteralPath $projectDir)) { throw "Missing package folder: $projectDir" }
    $version = Get-PackageVersion $projectDir $spec.Fallback
    $spec.Band = $spec.Band -f $version
    $master = Join-Path $Root "art\masters\$($spec.Folder)-1024.png"
    $icon = Join-Path $projectDir 'icon.png'
    New-PackageIcon $spec (Join-Path $sourceDir $spec.Source) $master $icon
    $check = [System.Drawing.Image]::FromFile($icon)
    try { if ($check.Width -ne 256 -or $check.Height -ne 256) { throw "$icon is $($check.Width)x$($check.Height), expected 256x256." } } finally { $check.Dispose() }
    Write-Log ("{0}: icon {1} (master {2}) from {3}, band '{4}'" -f $spec.Folder, (Get-FileHash -LiteralPath $icon -Algorithm SHA256).Hash.Substring(0, 12), (Get-FileHash -LiteralPath $master -Algorithm SHA256).Hash.Substring(0, 12), $spec.Source, $spec.Band)
}
Write-Log 'Done.'
