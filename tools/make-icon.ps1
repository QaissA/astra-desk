# Builds every app icon from landing\public\images\logo_v2.png:
#   assets\app.ico (16-256 px: exe, tray, windows, installer, Settings > Apps), assets\wizard-small.bmp (Setup wizard),
#   assets\logo-72.png (settings window badge, inlined into ui.html), landing favicon + nav logo.
# Usage: powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot
$src = [Drawing.Bitmap]::FromFile((Join-Path $root 'landing\public\images\logo_v2.png'))

# crop to the rounded tile: rows/columns that are mostly opaque (skips the soft glow and stray specks around it)
function Span([int]$n, [scriptblock]$opaqueCount, [int]$len) {
  $hit = 0..($n - 1) | Where-Object { (& $opaqueCount $_) -gt $len * 0.2 }
  ($hit | Measure-Object -Minimum -Maximum)
}
$cols = Span $src.Width { param($x) $c = 0; for ($y = 0; $y -lt $src.Height; $y += 4) { if ($src.GetPixel($x, $y).A -gt 200) { $c++ } }; $c * 4 } $src.Height
$rows = Span $src.Height { param($y) $c = 0; for ($x = 0; $x -lt $src.Width; $x += 4) { if ($src.GetPixel($x, $y).A -gt 200) { $c++ } }; $c * 4 } $src.Width
$side = [Math]::Max($cols.Maximum - $cols.Minimum, $rows.Maximum - $rows.Minimum) * 1.04 # 2% margin each side keeps the edge glow
$crop = [Drawing.RectangleF]::new(($cols.Minimum + $cols.Maximum - $side) / 2, ($rows.Minimum + $rows.Maximum - $side) / 2, $side, $side)
"tile crop: $crop"

function Resized([int]$s) {
  $b = New-Object Drawing.Bitmap $s, $s, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [Drawing.Graphics]::FromImage($b)
  $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.CompositingQuality = 'HighQuality'
  $g.DrawImage($src, [Drawing.RectangleF]::new(0, 0, $s, $s), $crop, [Drawing.GraphicsUnit]::Pixel); $g.Dispose(); $b
}
function Png([int]$s) { $b = Resized $s; $ms = New-Object IO.MemoryStream; $b.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $b.Dispose(); , $ms.ToArray() }
function Save-Png([int]$s, [string]$path) { [IO.File]::WriteAllBytes($path, (Png $s)); "wrote $path" }

# app.ico with PNG frames
$out = Join-Path $root 'assets\app.ico'
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = $sizes | ForEach-Object { , (Png $_) }
$fs = [IO.File]::Create($out); $w = New-Object IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)  # ICONDIR
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {                                     # ICONDIRENTRY per size
  $d = [byte]($(if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }))
  $w.Write($d); $w.Write($d); $w.Write([byte]0); $w.Write([byte]0)
  $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$frames[$i].Length); $w.Write([uint32]$offset)
  $offset += $frames[$i].Length
}
foreach ($f in $frames) { $w.Write($f) }
$w.Close(); "wrote $out"

# Setup wizard corner image: 32-bit BMP keeps the transparency
$bmp = Resized 110; $bmp.Save((Join-Path $root 'assets\wizard-small.bmp'), [Drawing.Imaging.ImageFormat]::Bmp); $bmp.Dispose(); "wrote assets\wizard-small.bmp"

Save-Png 72 (Join-Path $root 'assets\logo-72.png')
Save-Png 64 (Join-Path $root 'landing\app\icon.png')
Save-Png 96 (Join-Path $root 'landing\public\images\logo-small.png')
$src.Dispose()
