param(
    [Parameter(Mandatory = $true)]
    [string]$Database,

    [string]$HostUrl = "http://localhost:8181",

    [string]$OutputRoot,

    [string]$RunId
)

$ErrorActionPreference = 'Stop'

function Invoke-InfluxHttpQuery {
    param(
        [string]$BaseUrl,
        [string]$DatabaseName,
        [string]$Query,
        [string]$OutputPath
    )

    $token = $env:INFLUXDB3_AUTH_TOKEN
    if ([string]::IsNullOrWhiteSpace($token)) {
        $token = $env:INFLUXDB_TOKEN
    }

    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($token)) {
        $headers["Authorization"] = "Bearer $token"
    }

    $base = $BaseUrl.TrimEnd('/')
    $uri = "{0}/api/v3/query_sql?db={1}&format=csv&q={2}" -f `
        $base,
        [System.Uri]::EscapeDataString($DatabaseName),
        [System.Uri]::EscapeDataString($Query)

    $response = Invoke-WebRequest -Method Get -Uri $uri -Headers $headers -UseBasicParsing
    $response.Content | Set-Content -LiteralPath $OutputPath -Encoding UTF8
}

$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptRoot)

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot 'docs/evidence/ml/momento2/runs'
}

if ([string]::IsNullOrWhiteSpace($RunId)) {
    $RunId = Get-Date -Format 'yyyyMMdd-HHmmss'
}

$runRoot = Join-Path $OutputRoot $RunId
$influxOut = Join-Path $runRoot 'influx'
$summariesOut = Join-Path $runRoot 'summaries'

New-Item -ItemType Directory -Force -Path $influxOut, $summariesOut | Out-Null

$sqlRoot = Join-Path $scriptRoot 'influx'
$scripts = Get-ChildItem -LiteralPath $sqlRoot -Filter '*.sql' | Sort-Object Name

if (-not $scripts) {
    throw "Não foram encontrados scripts SQL em $sqlRoot."
}

$executed = New-Object System.Collections.Generic.List[string]
$influx = Get-Command influxdb3 -ErrorAction SilentlyContinue

foreach ($script in $scripts) {
    $query = Get-Content -LiteralPath $script.FullName -Raw
    $output = Join-Path $influxOut (($script.BaseName) + '.csv')

    Write-Host ("A executar {0}" -f $script.Name)
    if ($influx) {
        & $influx.Source query --database $Database --format csv $query | Set-Content -LiteralPath $output -Encoding UTF8

        if ($LASTEXITCODE -ne 0) {
            throw "influxdb3 falhou ao executar $($script.Name)."
        }
    }
    else {
        Invoke-InfluxHttpQuery -BaseUrl $HostUrl -DatabaseName $Database -Query $query -OutputPath $output
    }

    $executed.Add($script.Name)
}

$manifest = Join-Path $runRoot 'manifest.md'
$generatedFiles = Get-ChildItem -LiteralPath $influxOut -File | Sort-Object Name

@(
    '# Manifesto de Auditoria InfluxDB - Momento 2'
    ''
    ('Data/hora: {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
    ''
    '## Segurança'
    ''
    '- Execução read-only.'
    '- O token, se necessário, deve ser fornecido por variável de ambiente.'
    '- Nenhum token é gravado pelo runner.'
    ''
    '## Database/bucket'
    ''
    '```text'
    $Database
    '```'
    ''
    '## Endpoint'
    ''
    '```text'
    $HostUrl
    '```'
    ''
    '## Scripts corridos'
    ''
    ($executed | ForEach-Object { "- $_" })
    ''
    '## Ficheiros gerados'
    ''
    ($generatedFiles | ForEach-Object { "- influx/$($_.Name)" })
    ''
    '## Limitações'
    ''
    '- A evidência representa apenas o estado da base no momento da execução.'
    '- InfluxDB complementa, mas não substitui, a rastreabilidade PostgreSQL.'
) | Set-Content -LiteralPath $manifest -Encoding UTF8

Write-Host "Auditoria InfluxDB concluída."
Write-Host "Outputs: $runRoot"
