$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Genera app/icon.ico (anillo + rayo, paleta TrincherAI) en varias resoluciones.

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$outIco = Join-Path $root 'app\icon.ico'

$bg     = [System.Drawing.Color]::FromArgb(255, 13, 27, 42)    # #0D1B2A
$track  = [System.Drawing.Color]::FromArgb(255, 33, 48, 63)    # #21303F
$green  = [System.Drawing.Color]::FromArgb(255, 0, 255, 136)   # #00FF88

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $f = $size / 104.0

    # Fondo redondeado
    $bgPath = New-RoundedPath (2 * $f) (2 * $f) ($size - 4 * $f) ($size - 4 * $f) (22 * $f)
    $bgBrush = New-Object System.Drawing.SolidBrush($bg)
    $g.FillPath($bgBrush, $bgPath)

    # Anillo (track + arco verde)
    $c = $size / 2.0
    $r = 34 * $f
    $pw = 8 * $f
    $rect = New-Object System.Drawing.RectangleF(($c - $r), ($c - $r), (2 * $r), (2 * $r))

    $trackPen = New-Object System.Drawing.Pen($track, $pw)
    $g.DrawEllipse($trackPen, $rect)

    $arcPen = New-Object System.Drawing.Pen($green, $pw)
    $arcPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $arcPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($arcPen, $rect, -90, 255)

    # Rayo
    $pts = @(
        (New-Object System.Drawing.PointF((55 * $f), (33 * $f))),
        (New-Object System.Drawing.PointF((41 * $f), (56 * $f))),
        (New-Object System.Drawing.PointF((51 * $f), (56 * $f))),
        (New-Object System.Drawing.PointF((49 * $f), (71 * $f))),
        (New-Object System.Drawing.PointF((64 * $f), (47 * $f))),
        (New-Object System.Drawing.PointF((54 * $f), (47 * $f)))
    )
    $boltBrush = New-Object System.Drawing.SolidBrush($green)
    $g.FillPolygon($boltBrush, [System.Drawing.PointF[]]$pts)

    $g.Dispose()
    return $bmp
}

# Generar PNGs por tamaño
$sizes = @(16, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    $bmp.Dispose()
    $ms.Dispose()
}

# Ensamblar ICO (entradas PNG)
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0)            # reserved
$bw.Write([UInt16]1)            # type = icon
$bw.Write([UInt16]$sizes.Count) # count

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $len = $pngs[$i].Length
    $wh = 0
    if ($s -lt 256) { $wh = $s }
    $bw.Write([Byte]$wh)           # width
    $bw.Write([Byte]$wh)           # height
    $bw.Write([Byte]0)             # color count
    $bw.Write([Byte]0)             # reserved
    $bw.Write([UInt16]1)           # planes
    $bw.Write([UInt16]32)          # bit count
    $bw.Write([UInt32]$len)        # bytes in res
    $bw.Write([UInt32]$offset)     # offset
    $offset += $len
}
foreach ($png in $pngs) { $bw.Write($png) }
$bw.Flush()

New-Item -ItemType Directory -Path (Split-Path $outIco) -Force | Out-Null
[System.IO.File]::WriteAllBytes($outIco, $out.ToArray())
$bw.Dispose(); $out.Dispose()

Write-Host "Icono generado: $outIco ($((Get-Item $outIco).Length) bytes)"
