<#
.SYNOPSIS
Checks local tools and repository files needed by the NatureProtector baseline.

.DESCRIPTION
This script is read-only. It does not install packages, run restore, run
npm install, modify repository files, start containers, or delete data.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"

function Find-RepositoryRoot {
    $current = Get-Item -LiteralPath $PSScriptRoot

    while ($null -ne $current) {
        $solution = Join-Path $current.FullName "NatureProtector.sln"
        $compose = Join-Path $current.FullName "docker-compose.yml"

        if ((Test-Path -LiteralPath $solution) -and (Test-Path -LiteralPath $compose)) {
            return $current.FullName
        }

        $current = $current.Parent
    }

    throw "Could not locate repository root from $PSScriptRoot."
}

$script:Results = @()

function Add-Result {
    param(
        [ValidateSet("OK", "WARN", "FAIL")]
        [string]$Status,
        [string]$Name,
        [string]$Detail,
        [bool]$Required = $true
    )

    $script:Results += [pscustomobject]@{
        Status = $Status
        Name = $Name
        Detail = $Detail
        Required = $Required
    }

    $label = ("[{0}]" -f $Status).PadRight(7)
    Write-Host "$label $Name - $Detail"
}

function Read-DotEnv {
    param([string]$Path)

    $values = @{}
    if (-not (Test-Path -LiteralPath $Path)) {
        return $values
    }

    foreach ($rawLine in Get-Content -LiteralPath $Path) {
        $line = $rawLine.Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#") -or -not $line.Contains("=")) {
            continue
        }

        $parts = $line.Split("=", 2)
        $name = $parts[0].Trim()
        $value = $parts[1].Trim().Trim('"')
        if (-not [string]::IsNullOrWhiteSpace($name)) {
            $values[$name] = $value
        }
    }

    return $values
}

function Get-ConfigValue {
    param(
        [hashtable]$Values,
        [string]$Name,
        [string]$Fallback
    )

    if ($Values.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace([string]$Values[$Name])) {
        return [string]$Values[$Name]
    }

    return $Fallback
}

function Get-CommandPath {
    param([string]$Name)
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        return $null
    }

    return $command.Source
}

function Invoke-VersionCommand {
    param(
        [string]$Command,
        [string[]]$Arguments
    )

    try {
        $output = & $Command @Arguments 2>$null | Select-Object -First 1
        if ($LASTEXITCODE -ne 0 -and $null -eq $output) {
            return $null
        }

        return ($output | Out-String).Trim()
    }
    catch {
        return $null
    }
}

function Test-Tool {
    param(
        [string]$Name,
        [string]$Command,
        [string[]]$VersionArguments,
        [bool]$Required = $true
    )

    $path = Get-CommandPath $Command
    if ($null -eq $path) {
        if ($Required) {
            Add-Result "FAIL" $Name "$Command was not found on PATH." $true
        }
        else {
            Add-Result "WARN" $Name "$Command was not found on PATH." $false
        }
        return
    }

    $version = Invoke-VersionCommand $Command $VersionArguments
    if ([string]::IsNullOrWhiteSpace($version)) {
        $version = "found at $path"
    }

    Add-Result "OK" $Name $version $Required
}

function Invoke-ExternalCommand {
    param(
        [string]$Name,
        [string[]]$Arguments
    )

    try {
        $command = Get-Command $Name -ErrorAction Stop
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $command.Source
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true

        if ($Arguments.Count -gt 0) {
            $quotedArguments = foreach ($argument in $Arguments) {
                if ($argument -match '\s|"' ) {
                    '"' + ($argument -replace '"', '\"') + '"'
                }
                else {
                    $argument
                }
            }

            $startInfo.Arguments = ($quotedArguments -join " ")
        }

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        [void]$process.Start()
        $standardOutput = $process.StandardOutput.ReadToEnd()
        $standardError = $process.StandardError.ReadToEnd()
        $process.WaitForExit()

        $text = (($standardOutput + $standardError) | Out-String).Trim()
        $exitCode = $process.ExitCode
        if ($text -match "error during connect|Acesso negado|Access is denied|permission denied|Cannot connect") {
            $exitCode = 1
        }

        return [pscustomobject]@{
            ExitCode = $exitCode
            Output = $text
        }
    }
    catch {
        return [pscustomobject]@{
            ExitCode = 1
            Output = $_.Exception.Message
        }
    }
}

function Get-PortOwners {
    param([int]$Port)

    $owners = @()
    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    foreach ($connection in $connections) {
        try {
            $process = Get-Process -Id $connection.OwningProcess -ErrorAction Stop
            $owners += [pscustomobject]@{
                Id = $process.Id
                ProcessName = $process.ProcessName
            }
        }
        catch {
            $owners += [pscustomobject]@{
                Id = $connection.OwningProcess
                ProcessName = "<unknown>"
            }
        }
    }

    return $owners | Sort-Object Id -Unique
}

function Get-DockerPortOwners {
    $result = Invoke-ExternalCommand "docker" @("ps", "--format", "{{.Names}}|{{.Ports}}")
    if ($result.ExitCode -ne 0) {
        return @()
    }

    return @($result.Output -split "(`r`n|`n|`r)" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Test-BaselinePort {
    param(
        [string]$Name,
        [int]$Port,
        [string[]]$ExpectedContainers,
        [string[]]$DockerLines
    )

    $owners = @(Get-PortOwners -Port $Port)
    if ($owners.Count -eq 0) {
        Add-Result "OK" "Port $Name" "localhost:$Port is free before startup" $false
        return
    }

    $matchingContainer = $DockerLines | Where-Object {
        $line = $_
        ($ExpectedContainers | Where-Object { $line.StartsWith("$($_)|") }).Count -gt 0 -and
        ($line -match "0\.0\.0\.0:$Port->" -or $line -match "\[::\]:$Port->")
    } | Select-Object -First 1

    if ($matchingContainer) {
        Add-Result "OK" "Port $Name" "localhost:$Port is used by expected Docker container $($matchingContainer.Split('|')[0])" $false
        return
    }

    $summary = ($owners | ForEach-Object { "PID $($_.Id) ($($_.ProcessName))" }) -join ", "
    Add-Result "WARN" "Port $Name" "localhost:$Port is already in use by $summary; change .env or stop the process before startup" $false
}

function Test-DotNetSdkTarget {
    param([string]$RepoRoot)

    $propsPath = Join-Path $RepoRoot "Directory.Build.props"
    if (-not (Test-Path -LiteralPath $propsPath)) {
        Add-Result "WARN" ".NET target framework" "Directory.Build.props not found; could not infer expected SDK" $false
        return
    }

    try {
        $props = [xml](Get-Content -Raw -LiteralPath $propsPath)
        $targetFramework = [string]$props.Project.PropertyGroup.TargetFramework
        if ([string]::IsNullOrWhiteSpace($targetFramework) -or $targetFramework -notmatch '^net(?<major>\d+)\.') {
            Add-Result "WARN" ".NET target framework" "could not infer SDK major from TargetFramework '$targetFramework'" $false
            return
        }

        $expectedMajor = [int]$Matches["major"]
        $sdkVersion = Invoke-VersionCommand "dotnet" @("--version")
        if ([string]::IsNullOrWhiteSpace($sdkVersion) -or $sdkVersion -notmatch '^(?<major>\d+)\.') {
            Add-Result "WARN" ".NET SDK target" "could not parse dotnet --version output '$sdkVersion'" $false
            return
        }

        $actualMajor = [int]$Matches["major"]
        if ($actualMajor -eq $expectedMajor) {
            Add-Result "OK" ".NET SDK target" "$sdkVersion matches $targetFramework" $true
        }
        else {
            Add-Result "FAIL" ".NET SDK target" "$sdkVersion does not match required $targetFramework" $true
        }
    }
    catch {
        Add-Result "WARN" ".NET SDK target" "could not validate target framework: $($_.Exception.Message)" $false
    }
}

$repoRoot = Find-RepositoryRoot
Set-Location $repoRoot
$dotEnvPath = Join-Path $repoRoot ".env"
$dotEnvExamplePath = Join-Path $repoRoot ".env.example"
$envValues = Read-DotEnv $dotEnvPath
$exampleValues = Read-DotEnv $dotEnvExamplePath

Write-Host "NatureProtector local prerequisite check"
Write-Host "Repository root: $repoRoot"
Write-Host ""

Test-Tool "Git" "git" @("--version") $true
Test-Tool ".NET SDK" "dotnet" @("--version") $true
Test-DotNetSdkTarget $repoRoot
Test-Tool "Docker CLI" "docker" @("--version") $true

if (Get-CommandPath "docker") {
    $dockerInfo = Invoke-ExternalCommand "docker" @("info", "--format", "{{.ServerVersion}}")
    if ($dockerInfo.ExitCode -eq 0) {
        Add-Result "OK" "Docker engine" $dockerInfo.Output $true
    }
    else {
        Add-Result "FAIL" "Docker engine" "docker info failed: $($dockerInfo.Output)" $true
    }

    $composeVersion = Invoke-ExternalCommand "docker" @("compose", "version")
    if ($composeVersion.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($composeVersion.Output)) {
        Add-Result "OK" "Docker Compose v2" $composeVersion.Output $true
    }
    else {
        Add-Result "FAIL" "Docker Compose v2" "docker compose is not available: $($composeVersion.Output)" $true
    }
}

$psVersion = $PSVersionTable.PSVersion.ToString()
Add-Result "OK" "PowerShell" $psVersion $true

Test-Tool "Node.js" "node" @("--version") $true
Test-Tool "npm" "npm" @("--version") $true

if (Test-Path -LiteralPath (Join-Path $repoRoot ".env.example")) {
    Add-Result "OK" ".env.example" "found" $true
}
else {
    Add-Result "FAIL" ".env.example" "missing from repository root" $true
}

if (Test-Path -LiteralPath $dotEnvPath) {
    Add-Result "OK" ".env" "found" $true

    $influxToken = Get-ConfigValue $envValues "INFLUXDB_TOKEN" ""
    if ([string]::IsNullOrWhiteSpace($influxToken)) {
        Add-Result "WARN" "InfluxDB token config" "INFLUXDB_TOKEN is missing; up.ps1 will fail until .env is completed" $false
    }
    elseif ($influxToken -match "REPLACE_WITH|CHANGE_ME|<") {
        Add-Result "WARN" "InfluxDB token config" "INFLUXDB_TOKEN is still a placeholder; set a local apiv3_ token before running up.ps1" $false
    }
}
else {
    Add-Result "WARN" ".env" "missing; Setup-LocalEnvironment.ps1 or up.ps1 can create it from .env.example" $false
}

$frontendPackage = Join-Path $repoRoot "webUI\package.json"
if (Test-Path -LiteralPath $frontendPackage) {
    try {
        $package = Get-Content -Raw -LiteralPath $frontendPackage | ConvertFrom-Json
        if ($null -ne $package.scripts.build) {
            Add-Result "OK" "Frontend build script" $package.scripts.build $false
        }
        else {
            Add-Result "WARN" "Frontend build script" "webUI/package.json has no build script" $false
        }
    }
    catch {
        Add-Result "WARN" "Frontend package.json" "could not parse package.json: $($_.Exception.Message)" $false
    }
}
else {
    Add-Result "WARN" "Frontend package.json" "webUI/package.json not found" $false
}

$effectiveValues = if ($envValues.Count -gt 0) { $envValues } else { $exampleValues }
$rabbitAmqpPort = [int](Get-ConfigValue $effectiveValues "RABBITMQ_AMQP_PORT" "5672")
$rabbitManagementPort = [int](Get-ConfigValue $effectiveValues "RABBITMQ_MANAGEMENT_PORT" "15672")
$postgresPort = [int](Get-ConfigValue $effectiveValues "POSTGRES_PORT" "5433")
$influxPort = [int](Get-ConfigValue $effectiveValues "INFLUXDB_PORT" "8181")
$grafanaPort = [int](Get-ConfigValue $effectiveValues "GRAFANA_PORT" "3000")
$apiPort = [int](Get-ConfigValue $effectiveValues "BACKOFFICE_API_PORT" "5254")
$webPort = [int](Get-ConfigValue $effectiveValues "WEBUI_PORT" "5173")

$dockerLines = @(Get-DockerPortOwners)
Test-BaselinePort "RabbitMQ AMQP" $rabbitAmqpPort @("np-rabbitmq") $dockerLines
Test-BaselinePort "RabbitMQ management" $rabbitManagementPort @("np-rabbitmq") $dockerLines
Test-BaselinePort "PostgreSQL" $postgresPort @("np-postgres") $dockerLines
Test-BaselinePort "InfluxDB" $influxPort @("np-influxdb") $dockerLines
Test-BaselinePort "Grafana" $grafanaPort @("np-grafana") $dockerLines
Test-BaselinePort "Backoffice API" $apiPort @() $dockerLines
Test-BaselinePort "webUI" $webPort @() $dockerLines

Test-Tool "Strawberry Perl / Perl" "perl" @("--version") $false

$miktexCommand = Get-CommandPath "miktex"
$pdflatexCommand = Get-CommandPath "pdflatex"
if ($miktexCommand -or $pdflatexCommand) {
    $detail = "found"
    if ($pdflatexCommand) {
        $version = Invoke-VersionCommand "pdflatex" @("--version")
        if (-not [string]::IsNullOrWhiteSpace($version)) {
            $detail = $version
        }
    }
    Add-Result "OK" "MiKTeX / LaTeX" $detail $false
}
else {
    Add-Result "WARN" "MiKTeX / LaTeX" "not found; only needed for report/documentation workflows" $false
}

Write-Host ""
$requiredFailures = @($script:Results | Where-Object { $_.Status -eq "FAIL" -and $_.Required }).Count
$warnings = @($script:Results | Where-Object { $_.Status -eq "WARN" }).Count
$failures = @($script:Results | Where-Object { $_.Status -eq "FAIL" }).Count

Write-Host "Summary: $requiredFailures required failure(s), $failures total failure(s), $warnings warning(s)."

if ($requiredFailures -gt 0) {
    exit 1
}

exit 0
