# Pack a finished PNG (transparent) into the app icon set: tittle.png (512) + multi-size
# PNG-framed tittle.ico, written into src/Tittle/Assets. Usage: pack-icon.ps1 <source.png>
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$src = $args[0]
$assets = 'E:\Scripts\Tittle\src\Tittle\Assets'
if (-not (Test-Path -LiteralPath $src)) { throw "source not found: $src" }

$orig = [System.Drawing.Image]::FromFile($src)

function Resize([System.Drawing.Image]$img, [int]$sz) {
  $bmp = New-Object System.Drawing.Bitmap $sz, $sz, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.Clear([System.Drawing.Color]::Transparent)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.DrawImage($img, (New-Object System.Drawing.Rectangle 0, 0, $sz, $sz))
  $g.Dispose()
  return $bmp
}

# 512 master png
$png512 = Resize $orig 512
$png512.Save((Join-Path $assets 'tittle.png'), [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "wrote tittle.png (512)"

# multi-size png-framed ico
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = @()
foreach ($s in $sizes) {
  $b = Resize $orig $s
  $ms = New-Object System.IO.MemoryStream
  $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $pngs += , ($ms.ToArray())
  $b.Dispose(); $ms.Dispose()
}
$fs = [System.IO.File]::Create((Join-Path $assets 'tittle.ico'))
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$sizes.Count)   # ICONDIR
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]
  $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))   # width
  $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))   # height
  $bw.Write([byte]0); $bw.Write([byte]0)                     # colors, reserved
  $bw.Write([int16]1); $bw.Write([int16]32)                  # planes, bpp
  $bw.Write([int32]$pngs[$i].Length); $bw.Write([int32]$offset)
  $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush(); $bw.Close(); $fs.Close()
$png512.Dispose(); $orig.Dispose()
Write-Host "wrote tittle.ico ($($sizes -join ','))"
Write-Host 'done'
