param(
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\src\MarkdownMkII.App\Assets'),
    [string]$Preview
)

# Draws the app icon (the Markdown mark on a rounded gradient square) and writes every MSIX asset,
# the multi-size AppIcon.ico and, with -Preview, a single PNG. Run it again after changing the design.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$top = [Drawing.Color]::FromArgb(255, 77, 166, 255)
$bottom = [Drawing.Color]::FromArgb(255, 36, 118, 230)

function New-RoundedPath([float]$x, [float]$y, [float]$size, [float]$radius) {
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $d = $radius * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $size - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $size - $d, $y + $size - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

# Draws the icon into a square of side $size whose top-left corner is ($ox, $oy).
function Draw-Icon([Drawing.Graphics]$g, [float]$ox, [float]$oy, [float]$size) {
    $inset = if ($size -le 32) { 0 } else { $size * 0.04 }
    $side = $size - 2 * $inset
    $plate = New-RoundedPath ($ox + $inset) ($oy + $inset) $side ($side * 0.22)
    $brush = [Drawing.Drawing2D.LinearGradientBrush]::new(
        [Drawing.PointF]::new($ox, $oy), [Drawing.PointF]::new($ox + $size, $oy + $size), $top, $bottom)
    $g.FillPath($brush, $plate)

    function P([float]$u, [float]$v) { [Drawing.PointF]::new($ox + $inset + $u * $side, $oy + $inset + $v * $side) }
    $pen = [Drawing.Pen]::new([Drawing.Color]::White, $side * 0.095)
    $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
    $pen.StartCap = [Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $g.DrawLines($pen, [Drawing.PointF[]]@((P 0.205 0.69), (P 0.205 0.33), (P 0.375 0.52), (P 0.545 0.33), (P 0.545 0.69)))
    $g.DrawLine($pen, (P 0.735 0.33), (P 0.735 0.55))
    $white = [Drawing.SolidBrush]::new([Drawing.Color]::White)
    $g.FillPolygon($white, [Drawing.PointF[]]@((P 0.615 0.51), (P 0.855 0.51), (P 0.735 0.70)))
    $pen.Dispose(); $white.Dispose(); $brush.Dispose(); $plate.Dispose()
}

# $scale is the icon side relative to the shorter image side; the rest stays transparent.
function Save-Png([string]$file, [int]$width, [int]$height, [double]$scale = 1.0) {
    $bitmap = [Drawing.Bitmap]::new($width, $height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([Drawing.Color]::Transparent)
    $side = [Math]::Min($width, $height) * $scale
    Draw-Icon $g (($width - $side) / 2) (($height - $side) / 2) $side
    $g.Dispose()
    $bitmap.Save($file, [Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

if ($Preview) { Save-Png $Preview 256 256; return }

$OutputDir = [IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Force $OutputDir | Out-Null
Get-ChildItem $OutputDir -File | Where-Object Extension -in '.png', '.ico' | Remove-Item

foreach ($scale in @{ 100 = 1.0; 125 = 1.25; 150 = 1.5; 200 = 2.0; 400 = 4.0 }.GetEnumerator()) {
    $f = $scale.Value; $q = "scale-$($scale.Key)"
    Save-Png (Join-Path $OutputDir "Square44x44Logo.$q.png") ([int](44 * $f)) ([int](44 * $f))
    Save-Png (Join-Path $OutputDir "Square150x150Logo.$q.png") ([int](150 * $f)) ([int](150 * $f)) 0.66
    Save-Png (Join-Path $OutputDir "Wide310x150Logo.$q.png") ([int](310 * $f)) ([int](150 * $f)) 0.66
    Save-Png (Join-Path $OutputDir "StoreLogo.$q.png") ([int](50 * $f)) ([int](50 * $f))
    Save-Png (Join-Path $OutputDir "SplashScreen.$q.png") ([int](620 * $f)) ([int](300 * $f)) 0.5
    Save-Png (Join-Path $OutputDir "LockScreenLogo.$q.png") ([int](24 * $f)) ([int](24 * $f))
}
# Taskbar, Start and Explorer pick exact pixel sizes; "unplated" versions are shown without a tile behind.
foreach ($size in 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256) {
    foreach ($suffix in '', '_altform-unplated', '_altform-lightunplated') {
        Save-Png (Join-Path $OutputDir "Square44x44Logo.targetsize-$size$suffix.png") $size $size
    }
}

# ICO with PNG-compressed frames (supported since Windows Vista), largest last.
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = foreach ($size in $sizes) {
    $temp = [IO.Path]::GetTempFileName()
    Save-Png $temp $size $size
    , ([IO.File]::ReadAllBytes($temp))
    Remove-Item $temp
}
$ico = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($ico)
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([UInt16]1); $writer.Write([UInt16]32)
    $writer.Write([UInt32]$frames[$i].Length); $writer.Write([UInt32]$offset)
    $offset += $frames[$i].Length
}
foreach ($frame in $frames) { $writer.Write($frame) }
$writer.Flush()
[IO.File]::WriteAllBytes((Join-Path $OutputDir 'AppIcon.ico'), $ico.ToArray())
$writer.Dispose()
Write-Host "Icons written to $OutputDir"
