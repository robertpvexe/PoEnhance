param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '..\PoEnhance.App\Assets\poenhance-gem-source.png'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\PoEnhance.App\Assets')
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$resolvedSourcePath = (Resolve-Path -LiteralPath $SourcePath).Path
$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutputDirectory) | Out-Null

$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
$frames = [System.Collections.Generic.List[object]]::new()
$source = [System.Drawing.Bitmap]::FromFile($resolvedSourcePath)

try {
    foreach ($size in $sizes) {
        $margin = [Math]::Max(1, [int][Math]::Round($size * 0.0625))
        $subjectSize = $size - (2 * $margin)
        $bitmap = [System.Drawing.Bitmap]::new(
            $size,
            $size,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.DrawImage(
                    $source,
                    [System.Drawing.Rectangle]::new($margin, $margin, $subjectSize, $subjectSize),
                    0,
                    0,
                    $source.Width,
                    $source.Height,
                    [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally {
                $graphics.Dispose()
            }

            $stream = [System.IO.MemoryStream]::new()
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add([pscustomobject]@{
                Size = $size
                Bytes = $stream.ToArray()
            })
            $stream.Dispose()

            if ($size -eq 256) {
                $bitmap.Save(
                    (Join-Path $resolvedOutputDirectory 'poenhance-gem.png'),
                    [System.Drawing.Imaging.ImageFormat]::Png)
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }
}
finally {
    $source.Dispose()
}

$iconPath = Join-Path $resolvedOutputDirectory 'poenhance.ico'
$iconStream = [System.IO.File]::Create($iconPath)
$writer = [System.IO.BinaryWriter]::new($iconStream)

try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)

    $imageOffset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { [byte]0 } else { [byte]$frame.Size }
        $writer.Write($dimension)
        $writer.Write($dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length)
        $writer.Write([uint32]$imageOffset)
        $imageOffset += $frame.Bytes.Length
    }

    foreach ($frame in $frames) {
        $writer.Write($frame.Bytes)
    }
}
finally {
    $writer.Dispose()
    $iconStream.Dispose()
}

Write-Output "Generated $iconPath with sizes: $($sizes -join ', ')"
