param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,

    [string]$OutputRoot,

    [string]$RunId,

    [string]$ControlledValidationRunLabel
)

$ErrorActionPreference = 'Stop'

function Redact-ConnectionString {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ''
    }

    $redacted = $Value -replace '(?i)(Password|Pwd)\s*=\s*[^;]+', '$1=***'
    $redacted = $redacted -replace '(?i)(Token|AccessToken)\s*=\s*[^;]+', '$1=***'
    return $redacted
}

function Convert-ToPsqlConnectionString {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Value
    }

    if (($Value -match '^\s*(host|dbname|user|password|port)=') -and ($Value -notmatch ';')) {
        return $Value
    }

    $parts = @{}
    foreach ($segment in ($Value -split ';')) {
        if ([string]::IsNullOrWhiteSpace($segment)) {
            continue
        }

        $pair = $segment -split '=', 2
        if ($pair.Count -ne 2) {
            continue
        }

        $parts[$pair[0].Trim().ToLowerInvariant()] = $pair[1].Trim()
    }

    $mapped = [ordered]@{}
    if ($parts.ContainsKey('host')) { $mapped['host'] = $parts['host'] }
    if ($parts.ContainsKey('port')) { $mapped['port'] = $parts['port'] }
    if ($parts.ContainsKey('database')) { $mapped['dbname'] = $parts['database'] }
    if ($parts.ContainsKey('dbname')) { $mapped['dbname'] = $parts['dbname'] }
    if ($parts.ContainsKey('username')) { $mapped['user'] = $parts['username'] }
    if ($parts.ContainsKey('user id')) { $mapped['user'] = $parts['user id'] }
    if ($parts.ContainsKey('user')) { $mapped['user'] = $parts['user'] }
    if ($parts.ContainsKey('password')) { $mapped['password'] = $parts['password'] }
    if ($parts.ContainsKey('pwd')) { $mapped['password'] = $parts['pwd'] }

    if ($mapped.Count -eq 0) {
        return $Value
    }

    return (($mapped.GetEnumerator() | ForEach-Object {
        $escaped = $_.Value -replace "'", "\\'"
        "{0}='{1}'" -f $_.Key, $escaped
    }) -join ' ')
}

$psql = Get-Command psql -ErrorAction SilentlyContinue
if (-not $psql) {
    throw "psql não foi encontrado no PATH. Instale PostgreSQL client tools ou ajuste o PATH."
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
$postgresOut = Join-Path $runRoot 'postgres'
$summariesOut = Join-Path $runRoot 'summaries'

New-Item -ItemType Directory -Force -Path $postgresOut, $summariesOut | Out-Null

$sqlRoot = Join-Path $scriptRoot 'postgres'
$scripts = Get-ChildItem -LiteralPath $sqlRoot -Filter '*.sql' | Sort-Object Name

if (-not $scripts) {
    throw "Não foram encontrados scripts SQL em $sqlRoot."
}

$executed = New-Object System.Collections.Generic.List[string]
$psqlConnectionString = Convert-ToPsqlConnectionString $ConnectionString
$postgresOutForPsql = $postgresOut -replace '\\', '/'

foreach ($script in $scripts) {
    Write-Host ("A executar {0}" -f $script.Name)
    $psqlArgs = @(
        $psqlConnectionString,
        '-v',
        'ON_ERROR_STOP=1',
        '-v',
        "out_dir=$postgresOutForPsql"
    )

    if (-not [string]::IsNullOrWhiteSpace($ControlledValidationRunLabel)) {
        $psqlArgs += @(
            '-v',
            "run_label=$ControlledValidationRunLabel"
        )
    }

    $psqlArgs += @(
        '-f',
        $script.FullName
    )

    & $psql.Source @psqlArgs

    if ($LASTEXITCODE -ne 0) {
        throw "psql falhou ao executar $($script.Name)."
    }

    $executed.Add($script.Name)
}

$manifest = Join-Path $runRoot 'manifest.md'
$redacted = Redact-ConnectionString $ConnectionString
$generatedFiles = Get-ChildItem -LiteralPath $postgresOut -File | Sort-Object Name

@(
    '# Manifesto de Auditoria PostgreSQL - Momento 2'
    ''
    ('Data/hora: {0}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
    ''
    '## Segurança'
    ''
    '- Execução read-only.'
    '- Sem dumps completos.'
    '- Password/token redigidos no manifesto.'
    ''
    '## Connection string redigida'
    ''
    '```text'
    $redacted
    '```'
    ''
    '## Scripts corridos'
    ''
    ($executed | ForEach-Object { "- $_" })
    ''
    '## Filtros'
    ''
    if ([string]::IsNullOrWhiteSpace($ControlledValidationRunLabel)) {
        '- Controlled validation run label: <none>'
    } else {
        "- Controlled validation run label: $ControlledValidationRunLabel"
    }
    ''
    '## Ficheiros gerados'
    ''
    ($generatedFiles | ForEach-Object { "- postgres/$($_.Name)" })
    ''
    '## Limitações'
    ''
    '- A evidência representa apenas o estado da base no momento da execução.'
    '- M3 continua dependente de dados negativos suficientes.'
    '- M5 de falhas depende de rejeições, quarentenas, retries e correlação.'
) | Set-Content -LiteralPath $manifest -Encoding UTF8

Write-Host "Auditoria PostgreSQL concluída."
Write-Host "Outputs: $runRoot"
