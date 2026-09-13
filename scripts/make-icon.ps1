# Gera assets/pep-cli.ico (16..256, entradas PNG) e assets/pep-cli.png (256) com visual de terminal DOS/CLI.
# Compatível com Windows PowerShell 5.1 (System.Drawing).
param(
  [string]$OutputDir = (Join-Path $PSScriptRoot "..\assets")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$screenColor = [System.Drawing.ColorTranslator]::FromHtml("#0C1418")
$titleColor = [System.Drawing.ColorTranslator]::FromHtml("#16242A")
$borderColor = [System.Drawing.ColorTranslator]::FromHtml("#3FC1C9")
$promptTop = [System.Drawing.ColorTranslator]::FromHtml("#3FC1C9")
$promptBottom = [System.Drawing.ColorTranslator]::FromHtml("#5CC596")
$labelColor = [System.Drawing.ColorTranslator]::FromHtml("#9FB7BD")
$dotColors = @("#E5635B", "#E8B64C", "#5CC596")

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $r * 2
  $path.AddArc($x, $y, $d, $d, 180, 90)
  $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
  $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
  $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
  $path.CloseFigure()
  return $path
}

function Draw-Prompt($g, [int]$size, [single]$fontPx, [single]$areaTop, [single]$areaHeight) {
  $font = New-Object System.Drawing.Font("Consolas", $fontPx, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
  $format = [System.Drawing.StringFormat]::GenericTypographic
  $text = ">_"
  $measured = $g.MeasureString($text, $font, 1000, $format)
  $x = ($size - $measured.Width) / 2
  $y = $areaTop + ($areaHeight - $measured.Height) / 2
  $rect = New-Object System.Drawing.RectangleF($x, $y, $measured.Width, $measured.Height)
  $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $promptTop, $promptBottom, [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
  $g.DrawString($text, $font, $brush, $x, $y, $format)
  $brush.Dispose()
  $font.Dispose()
}

function New-IconBitmap([int]$size) {
  $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.Clear([System.Drawing.Color]::Transparent)

  $border = [Math]::Max(1.0, $size / 40.0)
  $radius = [Math]::Max(2.0, $size * 0.16)
  $inset = $border / 2
  $outer = New-RoundedPath $inset $inset ($size - $border) ($size - $border) $radius
  $screenBrush = New-Object System.Drawing.SolidBrush($screenColor)
  $g.FillPath($screenBrush, $outer)

  if ($size -ge 48) {
    $barHeight = [single]($size * 0.2)
    $g.SetClip($outer)
    $titleBrush = New-Object System.Drawing.SolidBrush($titleColor)
    $g.FillRectangle($titleBrush, 0, 0, $size, $barHeight)
    $titleBrush.Dispose()
    $g.ResetClip()

    $dot = [single]($size * 0.07)
    $gap = [single]($size * 0.045)
    $dotY = ($barHeight - $dot) / 2 + $border / 2
    $dotX = [single]($size * 0.1)
    foreach ($hex in $dotColors) {
      $dotBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($hex))
      $g.FillEllipse($dotBrush, $dotX, $dotY, $dot, $dot)
      $dotBrush.Dispose()
      $dotX += $dot + $gap
    }

    $labelFont = New-Object System.Drawing.Font("Segoe UI", [single]($size * 0.11), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $format = [System.Drawing.StringFormat]::GenericTypographic
    $labelSize = $g.MeasureString("PEP", $labelFont, 1000, $format)
    $labelBrush = New-Object System.Drawing.SolidBrush($labelColor)
    $g.DrawString("PEP", $labelFont, $labelBrush, ($size * 0.9 - $labelSize.Width), (($barHeight - $labelSize.Height) / 2 + $border / 2), $format)
    $labelBrush.Dispose()
    $labelFont.Dispose()

    $linePen = New-Object System.Drawing.Pen($borderColor, [single][Math]::Max(1.0, $size / 128.0))
    $linePen.Color = [System.Drawing.Color]::FromArgb(90, $borderColor)
    $g.DrawLine($linePen, $border, $barHeight, $size - $border, $barHeight)
    $linePen.Dispose()

    Draw-Prompt $g $size ([single]($size * 0.5)) $barHeight ([single]($size - $barHeight))
  }
  else {
    # Tamanhos pequenos: apenas ">_" ocupando o quadrado, sem título.
    $fontPx = if ($size -le 16) { 11 } else { 16 }
    Draw-Prompt $g $size ([single]$fontPx) 0 ([single]$size)
  }

  $pen = New-Object System.Drawing.Pen($borderColor, [single]$border)
  $g.DrawPath($pen, $outer)
  $pen.Dispose()
  $screenBrush.Dispose()
  $outer.Dispose()
  $g.Dispose()
  return $bmp
}

function Get-PngBytes($bmp) {
  $stream = New-Object System.IO.MemoryStream
  $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
  $bytes = $stream.ToArray()
  $stream.Dispose()
  return ,$bytes
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
  $bmp = New-IconBitmap $size
  $images += ,@{ Size = $size; Bytes = (Get-PngBytes $bmp) }
  if ($size -eq 256) { $bmp.Save((Join-Path $OutputDir "pep-cli.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
  $bmp.Dispose()
}

# ICO: ICONDIR (6 bytes) + ICONDIRENTRY (16 bytes cada) + dados PNG.
$icoPath = Join-Path $OutputDir "pep-cli.ico"
$file = [System.IO.File]::Create($icoPath)
$writer = New-Object System.IO.BinaryWriter($file)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
  $dim = if ($image.Size -ge 256) { 0 } else { $image.Size }
  $writer.Write([byte]$dim)
  $writer.Write([byte]$dim)
  $writer.Write([byte]0)
  $writer.Write([byte]0)
  $writer.Write([UInt16]1)
  $writer.Write([UInt16]32)
  $writer.Write([UInt32]$image.Bytes.Length)
  $writer.Write([UInt32]$offset)
  $offset += $image.Bytes.Length
}
foreach ($image in $images) { $writer.Write([byte[]]$image.Bytes) }
$writer.Dispose()
$file.Dispose()

Write-Host "Gerado: $(Resolve-Path $icoPath)"
Write-Host "Gerado: $(Resolve-Path (Join-Path $OutputDir 'pep-cli.png'))"
