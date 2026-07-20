[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ScreenshotRoot,
    [Parameter(Mandatory=$true)][string]$OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

function New-CompositeFigure {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][object[]]$Panels
    )

    $available = @(
        $Panels | Where-Object {
            Test-Path -LiteralPath (Join-Path $ScreenshotRoot ([string]$_.File)) -PathType Leaf
        }
    )
    if ($available.Count -eq 0) { return $null }

    $canvasWidth = 1800
    $margin = 36
    $gap = 28
    $titleHeight = 62
    $panelLabelHeight = 44
    $maxPanelHeight = 620

    $prepared = [System.Collections.Generic.List[object]]::new()
    $totalHeight = $margin + $titleHeight
    foreach ($panel in $available) {
        $path = Join-Path $ScreenshotRoot ([string]$panel.File)
        $image = [System.Drawing.Image]::FromFile($path)
        $availableWidth = $canvasWidth - ($margin * 2)
        $scale = [Math]::Min($availableWidth / [double]$image.Width, $maxPanelHeight / [double]$image.Height)
        if ($scale -gt 1) { $scale = 1 }
        $width = [int][Math]::Round($image.Width * $scale)
        $height = [int][Math]::Round($image.Height * $scale)
        $prepared.Add([pscustomobject]@{
            Panel = $panel
            Image = $image
            Width = $width
            Height = $height
        })
        $totalHeight += $panelLabelHeight + $height + $gap
    }
    $totalHeight += $margin - $gap

    $bitmap = [System.Drawing.Bitmap]::new($canvasWidth, $totalHeight)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::White)
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    $titleFont = [System.Drawing.Font]::new('Segoe UI', 24, [System.Drawing.FontStyle]::Bold)
    $labelFont = [System.Drawing.Font]::new('Segoe UI', 16, [System.Drawing.FontStyle]::Bold)
    $textBrush = [System.Drawing.Brushes]::Black
    $borderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(200, 203, 213, 225), 2)

    try {
        $graphics.DrawString([string]$Name, $titleFont, $textBrush, $margin, $margin)
        $y = $margin + $titleHeight
        $index = 0
        foreach ($item in $prepared) {
            $index++
            $label = "$index. $([string]$item.Panel.Label)"
            $graphics.DrawString($label, $labelFont, $textBrush, $margin, $y)
            $y += $panelLabelHeight
            $x = [int](($canvasWidth - $item.Width) / 2)
            $graphics.DrawImage($item.Image, $x, $y, $item.Width, $item.Height)
            $graphics.DrawRectangle($borderPen, $x, $y, $item.Width, $item.Height)
            $y += $item.Height + $gap
        }

        $destination = Join-Path $OutputRoot (($Name -replace '[^A-Za-z0-9._-]', '-') + '.png')
        $bitmap.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
        return $destination
    } finally {
        foreach ($item in $prepared) { $item.Image.Dispose() }
        $borderPen.Dispose()
        $labelFont.Dispose()
        $titleFont.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$figures = @(
    @{
        Name = 'figure-5x-configuration-and-execution'
        Panels = @(
            @{ File='hero-configuration.png'; Label='Configuração reproduzida do cenário C' },
            @{ File='hero-identity.png'; Label='Identificação inequívoca da hero run' },
            @{ File='hero-accounting.png'; Label='Contabilidade e settlement' }
        )
    },
    @{
        Name = 'figure-5y-quality-and-evaluation'
        Panels = @(
            @{ File='hero-quality.png'; Label='Qualidade, elegibilidade e perdas' },
            @{ File='hero-scientific-metrics.png'; Label='NP Score, FWI e KBDI' },
            @{ File='hero-vs-nominal-comparison.png'; Label='Comparação nominal versus degradada' }
        )
    },
    @{
        Name = 'figure-6x-evidence-traceability'
        Panels = @(
            @{ File='hero-evidence.png'; Label='Pacote exportável da execução' },
            @{ File='hero-query-quality.png'; Label='Consulta run-scoped' },
            @{ File='hero-evidence-catalog.png'; Label='Catálogo de evidência' }
        )
    }
)

$created = [System.Collections.Generic.List[object]]::new()
foreach ($figure in $figures) {
    $path = New-CompositeFigure -Name $figure.Name -Panels $figure.Panels
    if ($null -ne $path) {
        $created.Add([pscustomobject]@{
            figure = $figure.Name
            path = $path
            sha256 = ([string](Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash).ToLowerInvariant()
        })
    }
}
ConvertTo-Json -InputObject @($created) -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputRoot 'figure-register.json') -Encoding UTF8
Write-Host "POINT5_FIGURES_CREATED=$($created.Count)"
