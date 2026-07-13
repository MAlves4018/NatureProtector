[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectId,
    [string]$Region = 'europe-southwest1',
    [string]$Namespace = 'natureprotector-staging',
    [string]$SimulatorJobName = 'natureprotector-simulator',
    [string]$ApiServiceName = 'natureprotector-api',
    [string]$PreventionDeploymentName = 'natureprotector-prevention',
    [string]$EvidenceDirectory = 'artifacts/operational-audit/rabbitmq-health-phase3f/cloud-inventory'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$resolvedEvidenceRoot = if ([System.IO.Path]::IsPathRooted($EvidenceDirectory)) {
    [System.IO.Path]::GetFullPath($EvidenceDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $EvidenceDirectory))
}
$timestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$runDirectory = Join-Path $resolvedEvidenceRoot $timestamp
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

function Assert-Command {
    param([Parameter(Mandatory = $true)][string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Invoke-JsonCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [switch]$AllowFailure
    )

    $stdoutPath = Join-Path $runDirectory "$Name.json"
    $stderrPath = Join-Path $runDirectory "$Name.stderr.txt"
    Write-Host "> $Executable $($Arguments -join ' ')"
    $output = & $Executable @Arguments 2> $stderrPath
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        if (-not $AllowFailure) {
            throw "$Executable failed for '$Name' with exit code $exitCode. See $stderrPath"
        }
        return [ordered]@{ name = $Name; succeeded = $false; exitCode = $exitCode; path = $null }
    }

    $text = ($output -join [Environment]::NewLine)
    if ([string]::IsNullOrWhiteSpace($text)) { $text = 'null' }
    $text | Set-Content -LiteralPath $stdoutPath -Encoding utf8
    try { $null = $text | ConvertFrom-Json -Depth 50 }
    catch { throw "'$Name' did not return valid JSON: $($_.Exception.Message)" }
    return [ordered]@{ name = $Name; succeeded = $true; exitCode = 0; path = $stdoutPath }
}

function Read-JsonFile {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) { return $null }
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -Depth 50
}

function Find-EnvironmentVariables {
    param($Node)
    $found = [System.Collections.Generic.List[object]]::new()
    function Visit($Value) {
        if ($null -eq $Value) { return }
        if ($Value -is [System.Collections.IDictionary]) {
            if ($Value.Contains('name') -and $Value.Contains('value')) {
                $name = [string]$Value['name']
                if ($name -like 'RabbitMq__*') {
                    $found.Add([ordered]@{ name = $name; value = [string]$Value['value'] })
                }
            }
            foreach ($entry in $Value.GetEnumerator()) { Visit $entry.Value }
            return
        }
        if ($Value -is [pscustomobject]) {
            $properties = @($Value.PSObject.Properties)
            $nameProperty = $properties | Where-Object Name -eq 'name' | Select-Object -First 1
            $valueProperty = $properties | Where-Object Name -eq 'value' | Select-Object -First 1
            if ($null -ne $nameProperty -and $null -ne $valueProperty -and [string]$nameProperty.Value -like 'RabbitMq__*') {
                $found.Add([ordered]@{ name = [string]$nameProperty.Value; value = [string]$valueProperty.Value })
            }
            foreach ($property in $properties) { Visit $property.Value }
            return
        }
        if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
            foreach ($item in $Value) { Visit $item }
        }
    }
    Visit $Node
    return @($found | Sort-Object name, value -Unique)
}

Assert-Command gcloud
Assert-Command kubectl

$currentContext = (& kubectl config current-context 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($currentContext)) {
    throw 'No active kubectl context. This script deliberately does not run get-credentials or change kubeconfig.'
}
$currentContext | Set-Content -LiteralPath (Join-Path $runDirectory 'kubectl-context.txt') -Encoding utf8

$commands = [System.Collections.Generic.List[object]]::new()
$commands.Add((Invoke-JsonCommand -Name 'simulator-job' -Executable 'gcloud' -Arguments @(
    'run', 'jobs', 'describe', $SimulatorJobName,
    "--project=$ProjectId", "--region=$Region", '--format=json'
) -AllowFailure))
$commands.Add((Invoke-JsonCommand -Name 'simulator-executions' -Executable 'gcloud' -Arguments @(
    'run', 'jobs', 'executions', 'list', "--job=$SimulatorJobName",
    "--project=$ProjectId", "--region=$Region", '--format=json', '--limit=20'
) -AllowFailure))
$commands.Add((Invoke-JsonCommand -Name 'api-service' -Executable 'gcloud' -Arguments @(
    'run', 'services', 'describe', $ApiServiceName,
    "--project=$ProjectId", "--region=$Region", '--format=json'
) -AllowFailure))
$commands.Add((Invoke-JsonCommand -Name 'prevention-deployment' -Executable 'kubectl' -Arguments @(
    '--namespace', $Namespace, 'get', 'deployment', $PreventionDeploymentName, '-o', 'json'
)))
$commands.Add((Invoke-JsonCommand -Name 'prevention-replicasets' -Executable 'kubectl' -Arguments @(
    '--namespace', $Namespace, 'get', 'replicaset',
    '-l', 'app.kubernetes.io/name=natureprotector-prevention', '-o', 'json'
)))
$commands.Add((Invoke-JsonCommand -Name 'prevention-pods' -Executable 'kubectl' -Arguments @(
    '--namespace', $Namespace, 'get', 'pods',
    '-l', 'app.kubernetes.io/name=natureprotector-prevention', '-o', 'json'
)))
$commands.Add((Invoke-JsonCommand -Name 'rabbitmq-cluster' -Executable 'kubectl' -Arguments @(
    '--namespace', $Namespace, 'get', 'rabbitmqcluster', 'natureprotector-rabbitmq', '-o', 'json'
)))
$commands.Add((Invoke-JsonCommand -Name 'rabbitmq-policies' -Executable 'kubectl' -Arguments @(
    '--namespace', $Namespace, 'get', 'policy.rabbitmq.com', '-o', 'json'
) -AllowFailure))
$commands.Add((Invoke-JsonCommand -Name 'rabbitmq-users' -Executable 'kubectl' -Arguments @(
    '--namespace', $Namespace, 'get', 'user.rabbitmq.com', '-o', 'json'
) -AllowFailure))
$commands.Add((Invoke-JsonCommand -Name 'rabbitmq-permissions' -Executable 'kubectl' -Arguments @(
    '--namespace', $Namespace, 'get', 'permission.rabbitmq.com', '-o', 'json'
) -AllowFailure))

$simulator = Read-JsonFile (($commands | Where-Object name -eq 'simulator-job').path)
$api = Read-JsonFile (($commands | Where-Object name -eq 'api-service').path)
$prevention = Read-JsonFile (($commands | Where-Object name -eq 'prevention-deployment').path)
$replicaSets = Read-JsonFile (($commands | Where-Object name -eq 'prevention-replicasets').path)
$executions = Read-JsonFile (($commands | Where-Object name -eq 'simulator-executions').path)

$preventionImages = @()
if ($null -ne $prevention) {
    $preventionImages = @($prevention.spec.template.spec.containers | ForEach-Object { $_.image })
}
$oldReplicaSets = @()
if ($null -ne $replicaSets) {
    $oldReplicaSets = @($replicaSets.items | ForEach-Object {
        [ordered]@{
            name = $_.metadata.name
            revision = $_.metadata.annotations.'deployment.kubernetes.io/revision'
            replicas = $_.status.replicas
            readyReplicas = $_.status.readyReplicas
            images = @($_.spec.template.spec.containers | ForEach-Object { $_.image })
        }
    })
}
$runningExecutions = @()
if ($null -ne $executions) {
    $runningExecutions = @($executions | Where-Object {
        $conditions = @($_.status.conditions)
        -not ($conditions | Where-Object { $_.type -eq 'Completed' -and $_.status -eq 'True' })
    } | ForEach-Object { $_.metadata.name })
}

$summary = [ordered]@{
    schemaVersion = '1.0'
    collectedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    mode = 'READ_ONLY'
    cloudMutationsExecuted = $false
    projectId = $ProjectId
    region = $Region
    namespace = $Namespace
    kubectlContext = $currentContext
    rabbitMqEnvironment = [ordered]@{
        simulatorJob = Find-EnvironmentVariables $simulator
        apiService = Find-EnvironmentVariables $api
        preventionDeployment = Find-EnvironmentVariables $prevention
    }
    activeImages = [ordered]@{
        prevention = $preventionImages
    }
    historicalPreventionReplicaSets = $oldReplicaSets
    runningSimulatorExecutions = $runningExecutions
    commands = $commands
    gates = [ordered]@{
        simulatorRawDisabled = @((Find-EnvironmentVariables $simulator) | Where-Object {
            $_.name -eq 'RabbitMq__ObservabilityRawEnabled' -and $_.value -eq 'false'
        }).Count -ge 1
        preventionRawDisabled = @((Find-EnvironmentVariables $prevention) | Where-Object {
            $_.name -eq 'RabbitMq__ObservabilityRawEnabled' -and $_.value -eq 'false'
        }).Count -ge 1
        noRunningSimulatorExecutions = $runningExecutions.Count -eq 0
        kubeContextWasNotChanged = $true
    }
}
$summaryPath = Join-Path $runDirectory 'summary.json'
$summary | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $summaryPath -Encoding utf8

Write-Host 'PHASE3F_CLOUD_INVENTORY_READ_ONLY_COMPLETE'
Write-Host "Evidence directory: $runDirectory"
Write-Host 'No cloud resource, queue, binding, policy, secret or kubecontext was changed.'
