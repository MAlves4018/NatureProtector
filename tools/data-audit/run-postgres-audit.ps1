[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,

    [string]$OutputRoot,

    [string]$RunId,

    [string]$ControlledValidationRunLabel,

    [string]$PostgresContainer = $(if ($env:NP_POSTGRES_CONTAINER) { $env:NP_POSTGRES_CONTAINER } else { 'np-postgres' })
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Redact-ConnectionString {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return '' }
    $redacted = $Value -replace '(?i)(Password|Pwd)\s*=\s*[^;]+', '$1=***'
    $redacted = $redacted -replace '(?i)(Token|AccessToken)\s*=\s*[^;]+', '$1=***'
    return $redacted
}

function Convert-ToPsqlConnectionString {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $Value }
    if (($Value -match '^\s*(host|dbname|user|password|port)=') -and ($Value -notmatch ';')) { return $Value }

    $parts = @{}
    foreach ($segment in ($Value -split ';')) {
        if ([string]::IsNullOrWhiteSpace($segment)) { continue }
        $pair = $segment -split '=', 2
        if ($pair.Count -eq 2) { $parts[$pair[0].Trim().ToLowerInvariant()] = $pair[1].Trim() }
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
    if ($mapped.Count -eq 0) { return $Value }

    return (($mapped.GetEnumerator() | ForEach-Object {
        $escaped = ([string]$_.Value) -replace "'", "\\'"
        "{0}='{1}'" -f $_.Key, $escaped
    }) -join ' ')
}

function Convert-ConnectionStringToMap {
    param([string]$Value)
    $result = @{}
    foreach ($segment in ($Value -split ';')) {
        if ([string]::IsNullOrWhiteSpace($segment)) { continue }
        $pair = $segment -split '=', 2
        if ($pair.Count -eq 2) { $result[$pair[0].Trim().ToLowerInvariant()] = $pair[1].Trim() }
    }
    return $result
}

function Test-DockerContainerRunning {
    param([string]$Name)
    if ($null -eq (Get-Command docker -ErrorAction SilentlyContinue)) { return $false }
    $running = & docker inspect -f '{{.State.Running}}' $Name 2>$null
    return $LASTEXITCODE -eq 0 -and ([string]$running).Trim().ToLowerInvariant() -eq 'true'
}

$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptRoot)
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repoRoot 'docs/evidence/ml/momento2/runs' }
if ([string]::IsNullOrWhiteSpace($RunId)) { $RunId = Get-Date -Format 'yyyyMMdd-HHmmss' }

$runRoot = Join-Path $OutputRoot $RunId
$postgresOut = Join-Path $runRoot 'postgres'
$summariesOut = Join-Path $runRoot 'summaries'
New-Item -ItemType Directory -Force -Path $postgresOut, $summariesOut | Out-Null

$sqlRoot = Join-Path $scriptRoot 'postgres'
$scripts = @(Get-ChildItem -LiteralPath $sqlRoot -Filter '*.sql' | Sort-Object Name)
if ($scripts.Count -eq 0) { throw "Não foram encontrados scripts SQL em $sqlRoot." }

$localPsql = Get-Command psql -ErrorAction SilentlyContinue
$useDockerPsql = $null -eq $localPsql
if ($useDockerPsql -and -not (Test-DockerContainerRunning -Name $PostgresContainer)) {
    throw "psql não foi encontrado no PATH e o container PostgreSQL '$PostgresContainer' não está em execução. Inicie a infraestrutura local ou instale PostgreSQL client tools."
}

$executed = [System.Collections.Generic.List[string]]::new()
$psqlConnectionString = Convert-ToPsqlConnectionString $ConnectionString
$postgressOutForPsql = $postgresOut -replace '\\', '/'
$containerOutput = "/tmp/np-data-audit-$([guid]::NewGuid().ToString('N'))"
$connectionMap = Convert-ConnectionStringToMap $ConnectionString
$dockerUser = if ($connectionMap.ContainsKey('username')) { [string]$connectionMap['username'] } elseif ($connectionMap.ContainsKey('user')) { [string]$connectionMap['user'] } else { 'np' }
$dockerDatabase = if ($connectionMap.ContainsKey('database')) { [string]$connectionMap['database'] } elseif ($connectionMap.ContainsKey('dbname')) { [string]$connectionMap['dbname'] } else { 'natureprotector' }

try {
    if ($useDockerPsql) {
        & docker exec $PostgresContainer sh -lc "rm -rf '$containerOutput' && mkdir -p '$containerOutput'"
        if ($LASTEXITCODE -ne 0) { throw "Não foi possível preparar a pasta de auditoria no container $PostgresContainer." }
    }

    foreach ($script in $scripts) {
        Write-Host ("A executar {0} via {1}" -f $script.Name, $(if ($useDockerPsql) { "docker exec $PostgresContainer" } else { $localPsql.Source }))
        if ($useDockerPsql) {
            $dockerArgs = @('exec', '-i', $PostgresContainer, 'psql', '-U', $dockerUser, '-d', $dockerDatabase, '-v', 'ON_ERROR_STOP=1', '-v', "out_dir=$containerOutput")
            if (-not [string]::IsNullOrWhiteSpace($ControlledValidationRunLabel)) { $dockerArgs += @('-v', "run_label=$ControlledValidationRunLabel") }
            $sqlText = [System.IO.File]::ReadAllText($script.FullName)
            $sqlText | & docker @dockerArgs
        }
        else {
            $psqlArgs = @($psqlConnectionString, '-v', 'ON_ERROR_STOP=1', '-v', "out_dir=$postgressOutForPsql")
            if (-not [string]::IsNullOrWhiteSpace($ControlledValidationRunLabel)) { $psqlArgs += @('-v', "run_label=$ControlledValidationRunLabel") }
            $psqlArgs += @('-f', $script.FullName)
            & $localPsql.Source @psqlArgs
        }
        if ($LASTEXITCODE -ne 0) { throw "psql falhou ao executar $($script.Name)." }
        $executed.Add($script.Name) | Out-Null
    }

    if ($useDockerPsql) {
        & docker cp "${PostgresContainer}:$containerOutput/." $postgresOut
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível copiar os resultados da auditoria PostgreSQL para o host.' }
    }
}
finally {
    if ($useDockerPsql) { & docker exec $PostgresContainer sh -lc "rm -rf '$containerOutput'" *> $null }
}

$manifest = Join-Path $runRoot 'manifest.md'
$redacted = Redact-ConnectionString $ConnectionString
$generatedFiles = @(Get-ChildItem -LiteralPath $postgresOut -File | Sort-Object Name)
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
    '## Executor'
    ''
    $(if ($useDockerPsql) { "- Docker fallback: container $PostgresContainer." } else { "- Cliente local: $($localPsql.Source)." })
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
    $(if ([string]::IsNullOrWhiteSpace($ControlledValidationRunLabel)) { '- Controlled validation run label: <none>' } else { "- Controlled validation run label: $ControlledValidationRunLabel" })
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

Write-Host 'Auditoria PostgreSQL concluída.'
Write-Host "Outputs: $runRoot"
