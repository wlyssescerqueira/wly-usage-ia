Add-Type -AssemblyName System.Drawing
$iconRoot = Split-Path $PSScriptRoot -Parent
$iconPath = Join-Path $iconRoot 'Assets\usage.ico'
$iconSizes = @(16, 24, 32, 48, 64, 256)
$iconImages = @()
foreach ($iconSize in $iconSizes) {
    $bitmap = New-Object System.Drawing.Bitmap($iconSize, $iconSize)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $scale = $iconSize / 32.0
    $graphics.ScaleTransform($scale, $scale)
    $shape = New-Object System.Drawing.Drawing2D.GraphicsPath
    $shape.AddArc(1, 1, 12, 12, 180, 90)
    $shape.AddArc(19, 1, 12, 12, 270, 90)
    $shape.AddArc(19, 19, 12, 12, 0, 90)
    $shape.AddArc(1, 19, 12, 12, 90, 90)
    $shape.CloseFigure()
    $backgroundBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#4255ff'))
    $graphics.FillPath($backgroundBrush, $shape)
    $barBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $graphics.FillRectangle($barBrush, 7, 17, 4, 8)
    $graphics.FillRectangle($barBrush, 14, 12, 4, 13)
    $graphics.FillRectangle($barBrush, 21, 7, 4, 18)
    $memory = New-Object System.IO.MemoryStream
    $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
    $iconImages += ,($memory.ToArray())
    if ($iconSize -eq 256) { $bitmap.Save((Join-Path $iconRoot 'artifacts\tray-icon.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $memory.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $shape.Dispose(); $backgroundBrush.Dispose(); $barBrush.Dispose()
}
$file = [System.IO.File]::Create($iconPath)
$writer = New-Object System.IO.BinaryWriter($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$iconSizes.Count)
$offset = 6 + 16 * $iconSizes.Count
for ($index = 0; $index -lt $iconSizes.Count; $index++) {
    $dimension = if ($iconSizes[$index] -eq 256) { 0 } else { $iconSizes[$index] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$iconImages[$index].Length); $writer.Write([uint32]$offset)
    $offset += $iconImages[$index].Length
}
foreach ($iconImage in $iconImages) { $writer.Write([byte[]]$iconImage) }
$writer.Dispose(); $file.Dispose()
