<#
.SYNOPSIS
Compatibility local workspace entrypoint for NatureProtector.

.DESCRIPTION
Provides the existing Git-safe command surface for local setup, infrastructure
startup, validation, shutdown, and destructive reset flows. New clone-to-run
workflows should prefer scripts\np.ps1; this script is kept compatible for
existing operators and never creates or edits .env or .env.example.
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("setup", "up", "validate", "down", "reset", "help")]
    [string]$Command = "help",

    [ValidateSet("Quick", "Full", "Infrastructure", "Runtime", "Security", "PerformanceSmoke")]
    [string]$Profile = "Quick",

    [switch]$InstallMissing,
    [switch]$Yes,
    [switch]$NonInteractive,
    [switch]$PlanOnly,
    [switch]$Force,
    [switch]$StartRuntime,
    [switch]$OpenBrowser,
    [switch]$NoDependencyRestore,
    [switch]$NoPlaywrightInstall,
    [string]$Confirm
)

Import-Module (Join-Path $PSScriptRoot 'common/NatureProtector.Tooling.psd1') -Force -ErrorAction Stop

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ProgressRoot = Join-Path $RepoRoot "artifacts\mission-progress"
$SetupFingerprintPath = Join-Path $ProgressRoot "workspace-setup-fingerprint.json"

function Write-WorkspaceLine {
    param(
        [ValidateSet("INFO", "OK", "WARN", "FAIL", "PLAN")]
        [string]$Level,
        [string]$Message
    )

    Write-Host ("[{0}] {1}" -f $Level, $Message)
}

function Format-CommandLine {
    param(
        [string]$FileName,
        [string[]]$Arguments
    )

    $parts = @($FileName)
    foreach ($argument in $Arguments) {
        if ($argument -match '\s|"') {
            $parts += ('"' + ($argument -replace '"', '\"') + '"')
        }
        else {
            $parts += $argument
        }
    }

    return ($parts -join " ")
}

function Invoke-External {
    param(
        [string]$FileName,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory = $RepoRoot,
        [switch]$Required
    )

    $line = Format-CommandLine $FileName $Arguments
    if ($PlanOnly) {
        Write-WorkspaceLine "PLAN" "$line (cwd: $WorkingDirectory)"
        return 0
    }

    $commandInfo = Get-Command $FileName -ErrorAction Stop
    Push-Location $WorkingDirectory
    try {
        & $commandInfo.Source @Arguments
        $exitCode = if ($null -ne $LASTEXITCODE) { $LASTEXITCODE } else { 0 }
    }
    finally {
        Pop-Location
    }

    if ($Required -and $exitCode -ne 0) {
        throw "Command failed with exit code ${exitCode}: $line"
    }

    return $exitCode
}

function Get-CommandVersionText {
    param(
        [string]$FileName,
        [string[]]$Arguments
    )

    if (-not (Get-Command $FileName -ErrorAction SilentlyContinue)) {
        return $null
    }

    try {
        $output = & $FileName @Arguments 2>$null | Select-Object -First 1
        return ($output | Out-String).Trim()
    }
    catch {
        return $null
    }
}

function Add-PrerequisiteResult {
    param(
        [System.Collections.Generic.List[object]]$Results,
        [ValidateSet("OK", "WARN", "FAIL")]
        [string]$Status,
        [string]$Name,
        [string]$Detail,
        [bool]$Required = $true
    )

    $Results.Add([pscustomobject]@{
        Status = $Status
        Name = $Name
        Detail = $Detail
        Required = $Required
    }) | Out-Null
}

function Test-VersionPrefix {
    param(
        [string]$Version,
        [string[]]$AllowedPrefixes
    )

    foreach ($prefix in $AllowedPrefixes) {
        if ($Version.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Test-TcpPort {
    param([int]$Port)

    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
        $listener.Start()
        $listener.Stop()
        return $true
    }
    catch {
        return $false
    }
}

function Get-WorkspaceRelativePath {
    param([string]$Path)

    $root = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd('\', '/')
    $fullPath = [System.IO.Path]::GetFullPath($Path)

    if ($fullPath.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($root.Length).TrimStart('\', '/')
    }

    return $fullPath
}

function Test-WorkspacePrerequisites {
    param(
        [switch]$RequireDocker,
        [switch]$RequireDotEnv
    )

    if ($PlanOnly) {
        Write-WorkspaceLine "PLAN" "Check operating system."
        Write-WorkspaceLine "PLAN" "Check PowerShell version."
        Write-WorkspaceLine "PLAN" "Check repository-pinned .NET SDK."
        Write-WorkspaceLine "PLAN" "Check repository-supported Node.js version."
        Write-WorkspaceLine "PLAN" "Check npm availability."
        Write-WorkspaceLine "PLAN" "Git executable: report availability without executing Git."
        Write-WorkspaceLine "PLAN" "Check .env.example existence."

        if ($RequireDocker) {
            Write-WorkspaceLine "PLAN" "Check Docker CLI, engine and Docker Compose."
        }
        else {
            Write-WorkspaceLine "PLAN" "Docker checks are optional for this command."
        }

        if ($RequireDotEnv) {
            Write-WorkspaceLine "PLAN" "Require manually managed .env without creating or editing it."
        }
        else {
            Write-WorkspaceLine "PLAN" "Report .env state without requiring or modifying it."
        }

        Write-WorkspaceLine "PLAN" "Check local service port availability."
        Write-WorkspaceLine "PLAN" "Check disk space and Playwright configuration."

        return @()
    }

    $results = [System.Collections.Generic.List[object]]::new()

    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        Add-PrerequisiteResult $results "OK" "Operating system" "Windows detected."
    }
    else {
        Add-PrerequisiteResult $results "WARN" "Operating system" "Primary local scripts are validated on Windows." $false
    }

    Add-PrerequisiteResult $results "OK" "PowerShell" $PSVersionTable.PSVersion.ToString()

    $globalJsonPath = Join-Path $RepoRoot "global.json"
    $expectedDotnet = $null
    if (Test-Path -LiteralPath $globalJsonPath) {
        $expectedDotnet = ((Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json).sdk.version)
    }

    $dotnetVersion = Get-CommandVersionText "dotnet" @("--version")
    if ([string]::IsNullOrWhiteSpace($dotnetVersion)) {
        Add-PrerequisiteResult $results "FAIL" ".NET SDK" "dotnet was not found on PATH."
    }
    elseif ($expectedDotnet -and -not $dotnetVersion.StartsWith(($expectedDotnet.Substring(0, [Math]::Min(5, $expectedDotnet.Length))), [System.StringComparison]::OrdinalIgnoreCase)) {
        Add-PrerequisiteResult $results "WARN" ".NET SDK" "Found $dotnetVersion; repository pins $expectedDotnet."
    }
    else {
        Add-PrerequisiteResult $results "OK" ".NET SDK" "Found $dotnetVersion."
    }

    $nodeVersion = Get-CommandVersionText "node" @("--version")
    if ([string]::IsNullOrWhiteSpace($nodeVersion)) {
        Add-PrerequisiteResult $results "FAIL" "Node.js" "node was not found on PATH."
    }
    elseif (Test-VersionPrefix $nodeVersion @("v20.17.", "v22.16.", "v26.")) {
        Add-PrerequisiteResult $results "OK" "Node.js" "Found $nodeVersion."
    }
    else {
        Add-PrerequisiteResult $results "FAIL" "Node.js" "Found $nodeVersion; expected 20.17.x or 22.16.x for this repository."
    }

    $npmVersion = Get-CommandVersionText "npm" @("--version")
    if ([string]::IsNullOrWhiteSpace($npmVersion)) {
        Add-PrerequisiteResult $results "FAIL" "npm" "npm was not found on PATH."
    }
    else {
        Add-PrerequisiteResult $results "OK" "npm" "Found $npmVersion."
    }

    if (Get-Command "git" -ErrorAction SilentlyContinue) {
        Add-PrerequisiteResult $results "OK" "Git executable" "Found on PATH without executing Git."
    }
    else {
        Add-PrerequisiteResult $results "WARN" "Git executable" "Not found on PATH; this script does not execute Git commands." $false
    }

    $dockerCommand = Get-Command "docker" -ErrorAction SilentlyContinue
    if ($dockerCommand) {
        Add-PrerequisiteResult $results "OK" "Docker CLI" "Found $($dockerCommand.Source)."

        $dockerVersion = Get-CommandVersionText "docker" @("version", "--format", "{{.Server.Version}}")
        if ([string]::IsNullOrWhiteSpace($dockerVersion)) {
            $status = if ($RequireDocker) { "FAIL" } else { "WARN" }
            Add-PrerequisiteResult $results $status "Docker engine" "Docker CLI is present, but the engine is not reachable." ([bool]$RequireDocker)
        }
        else {
            Add-PrerequisiteResult $results "OK" "Docker engine" "Found server $dockerVersion."
        }

        $composeVersion = Get-CommandVersionText "docker" @("compose", "version", "--short")
        if ([string]::IsNullOrWhiteSpace($composeVersion)) {
            $status = if ($RequireDocker) { "FAIL" } else { "WARN" }
            Add-PrerequisiteResult $results $status "Docker Compose" "Docker Compose v2 is not available." ([bool]$RequireDocker)
        }
        else {
            Add-PrerequisiteResult $results "OK" "Docker Compose" "Found $composeVersion."
        }
    }
    else {
        $status = if ($RequireDocker) { "FAIL" } else { "WARN" }
        Add-PrerequisiteResult $results $status "Docker CLI" "docker was not found on PATH." ([bool]$RequireDocker)
    }

    $dotEnvPath = Join-Path $RepoRoot ".env"
    $dotEnvExamplePath = Join-Path $RepoRoot ".env.example"
    if (Test-Path -LiteralPath $dotEnvExamplePath) {
        Add-PrerequisiteResult $results "OK" ".env.example" "Template exists."
    }
    else {
        Add-PrerequisiteResult $results "FAIL" ".env.example" "Template is missing."
    }

    if (Test-Path -LiteralPath $dotEnvPath) {
        Add-PrerequisiteResult $results "OK" ".env" "Local file exists. Values are not printed."
    }
    else {
        $status = if ($RequireDotEnv) { "FAIL" } else { "WARN" }
        Add-PrerequisiteResult $results $status ".env" "Local file is missing. Create it manually from .env.example; this script will not create it." ([bool]$RequireDotEnv)
    }

    $envValues = Read-NpDotEnv -Path $dotEnvPath -QuoteHandling BothTrim
    $ports = @(
        [pscustomobject]@{ Name = "PostgreSQL"; Port = [int](Get-NpConfigValue $envValues "POSTGRES_PORT" "5433") },
        [pscustomobject]@{ Name = "RabbitMQ"; Port = [int](Get-NpConfigValue $envValues "RABBITMQ_PORT" "5672") },
        [pscustomobject]@{ Name = "RabbitMQ management"; Port = [int](Get-NpConfigValue $envValues "RABBITMQ_MANAGEMENT_PORT" "15672") },
        [pscustomobject]@{ Name = "InfluxDB"; Port = [int](Get-NpConfigValue $envValues "INFLUXDB_HTTP_PORT" "8181") }
    )

    foreach ($portInfo in $ports) {
        if (Test-TcpPort $portInfo.Port) {
            Add-PrerequisiteResult $results "OK" "$($portInfo.Name) port" "localhost:$($portInfo.Port) is available before startup." $false
        }
        else {
            Add-PrerequisiteResult $results "WARN" "$($portInfo.Name) port" "localhost:$($portInfo.Port) is already in use; this can be valid if local infrastructure is already running." $false
        }
    }

    $drive = Get-PSDrive -Name ([System.IO.Path]::GetPathRoot($RepoRoot).Substring(0, 1))
    $freeGb = [Math]::Round($drive.Free / 1GB, 1)
    if ($freeGb -lt 5) {
        Add-PrerequisiteResult $results "WARN" "Disk space" "$freeGb GB free on $($drive.Name): drive." $false
    }
    else {
        Add-PrerequisiteResult $results "OK" "Disk space" "$freeGb GB free on $($drive.Name): drive."
    }

    if (Test-Path -LiteralPath (Join-Path $RepoRoot "webUI\playwright.config.ts")) {
        Add-PrerequisiteResult $results "OK" "Playwright config" "webUI/playwright.config.ts exists." $false
    }
    else {
        Add-PrerequisiteResult $results "WARN" "Playwright config" "webUI/playwright.config.ts is missing." $false
    }

    foreach ($result in $results) {
        Write-WorkspaceLine $result.Status "$($result.Name): $($result.Detail)"
    }

    $failedRequired = @($results | Where-Object { $_.Status -eq "FAIL" -and $_.Required })
    if ($failedRequired.Count -gt 0) {
        throw "Required prerequisites failed: $($failedRequired.Name -join ', ')"
    }

    return $results
}

function Get-WorkspaceFingerprintInputs {
    $patterns = @(
        "global.json",
        "NuGet.Config",
        "Directory.Build.props",
        "Directory.Packages.props",
        ".config\dotnet-tools.json",
        "coverage.runsettings",
        "package.json",
        "package-lock.json",
        ".node-version",
        ".nvmrc",
        "webUI\package.json",
        "webUI\package-lock.json",
        "webUI\playwright.config.ts",
        "webUI\biome.json",
        "src\**\*.csproj",
        "tests\**\*.csproj",
        "benchmarks\**\*.csproj"
    )

    $files = [System.Collections.Generic.List[string]]::new()
    foreach ($pattern in $patterns) {
        Get-ChildItem -Path $RepoRoot -Recurse -File -Filter (Split-Path $pattern -Leaf) -ErrorAction SilentlyContinue |
            Where-Object {
                $relative = Get-WorkspaceRelativePath $_.FullName
                $relative -like $pattern
            } |
            ForEach-Object { $files.Add($_.FullName) | Out-Null }
    }

    return $files | Sort-Object -Unique
}

function Get-WorkspaceFingerprint {
    $entries = foreach ($file in Get-WorkspaceFingerprintInputs) {
        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file
        [pscustomobject]@{
            Path = Get-WorkspaceRelativePath $file
            Hash = $hash.Hash
        }
    }

    $payload = [pscustomobject]@{
        GeneratedAt = (Get-Date).ToString("o")
        Entries = @($entries)
    }

    return $payload
}

function Save-WorkspaceFingerprint {
    if ($PlanOnly) {
        Write-WorkspaceLine "PLAN" "Write setup fingerprint to $SetupFingerprintPath"
        return
    }

    New-Item -ItemType Directory -Force -Path $ProgressRoot | Out-Null
    Get-WorkspaceFingerprint | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $SetupFingerprintPath -Encoding UTF8
    Write-WorkspaceLine "OK" "Updated setup fingerprint at $SetupFingerprintPath."
}

function Invoke-WorkspaceSetup {
    Test-WorkspacePrerequisites | Out-Null

    if ($InstallMissing) {
        $installer = Join-Path $RepoRoot "scripts\setup\Install-LocalPrerequisites.ps1"
        if (-not (Test-Path -LiteralPath $installer)) {
            throw "InstallMissing was requested, but $installer was not found."
        }

        $installArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $installer)
        if ($Yes) { $installArgs += "-Yes" }
        if ($NonInteractive) { $installArgs += "-NonInteractive" }
        Invoke-External "pwsh" $installArgs $RepoRoot -Required | Out-Null
    }

    Invoke-External "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "scripts\dotnet\Use-RepoDotnetEnvironment.ps1"), "-Quiet") $RepoRoot -Required | Out-Null

    if (-not $NoDependencyRestore) {
        Invoke-External "dotnet" @("tool", "restore") $RepoRoot -Required | Out-Null
        Invoke-External "dotnet" @("restore", ".\NatureProtector.sln", "--configfile", ".\NuGet.Config", "--nologo") $RepoRoot -Required | Out-Null
        Invoke-External "npm" @("ci") (Join-Path $RepoRoot "webUI") -Required | Out-Null

        if (-not $NoPlaywrightInstall) {
            Invoke-External "npm" @("exec", "--", "playwright", "install", "chromium") (Join-Path $RepoRoot "webUI") -Required | Out-Null
        }
    }
    else {
        Write-WorkspaceLine "INFO" "Dependency restore skipped by -NoDependencyRestore."
    }

    Save-WorkspaceFingerprint
}

function Invoke-WorkspaceUp {
    Test-WorkspacePrerequisites -RequireDocker -RequireDotEnv | Out-Null
    Invoke-WorkspaceSetup
    Invoke-External "docker" @("compose", "--project-directory", $RepoRoot, "-f", (Join-Path $RepoRoot "docker-compose.yml"), "config", "--quiet") $RepoRoot -Required | Out-Null
    Invoke-External "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "infra\scripts\up.ps1"), "-SkipWorkspacePreparation") $RepoRoot -Required | Out-Null
    Invoke-External "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "scripts\postgres\bootstrap-control-plane.ps1")) $RepoRoot -Required | Out-Null
    Invoke-External "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "scripts\setup\Test-LocalBaseline.ps1"), "-InfrastructureOnly") $RepoRoot -Required | Out-Null

    if ($StartRuntime) {
        Invoke-External "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "scripts\runtime\Start-LocalRuntime.ps1"), "-SkipDocker") $RepoRoot -Required | Out-Null
    }

    if ($OpenBrowser) {
        $webUrl = "http://localhost:5173"
        if ($PlanOnly) {
            Write-WorkspaceLine "PLAN" "Open $webUrl"
        }
        else {
            Start-Process $webUrl
        }
    }
}

function Stop-LocalNatureProtectorProcesses {
    $escapedRoot = [regex]::Escape($RepoRoot)
    $processes = Get-CimInstance Win32_Process |
        Where-Object {
            $_.CommandLine -and
            $_.CommandLine -match $escapedRoot -and
            $_.CommandLine -match "NatureProtector" -and
            ($_.Name -in @("dotnet.exe", "node.exe", "npm.exe", "powershell.exe", "pwsh.exe"))
        }

    foreach ($process in $processes) {
        $message = "Stop process $($process.ProcessId) $($process.Name)"
        if ($PlanOnly) {
            Write-WorkspaceLine "PLAN" $message
            continue
        }

        try {
            Stop-Process -Id $process.ProcessId -Force:$Force -ErrorAction Stop
            Write-WorkspaceLine "OK" $message
        }
        catch {
            Write-WorkspaceLine "WARN" "$message failed: $($_.Exception.Message)"
        }
    }
}

function Invoke-WorkspaceDown {
    Stop-LocalNatureProtectorProcesses
    Invoke-External "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "infra\scripts\down.ps1")) $RepoRoot -Required | Out-Null
}

function Invoke-WorkspaceReset {
    if ($Confirm -ne "RESET_LOCAL_INFRA") {
        throw "Reset requires -Confirm RESET_LOCAL_INFRA. This guard is required even with -PlanOnly."
    }

    Stop-LocalNatureProtectorProcesses
    Invoke-External "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "infra\scripts\reset-local-infra.ps1")) $RepoRoot -Required | Out-Null
}

function Invoke-WorkspaceValidate {
    Test-WorkspacePrerequisites -RequireDocker:($Profile -in @("Infrastructure", "Runtime")) -RequireDotEnv:($Profile -in @("Infrastructure", "Runtime")) | Out-Null
    Write-WorkspaceLine "INFO" "Validation profile: $Profile"

    switch ($Profile) {
        "Quick" {
            Invoke-External "dotnet" @("build", ".\NatureProtector.sln", "-c", "Release", "--no-restore", "--nologo", "-v", "minimal", "-m:1") $RepoRoot -Required | Out-Null
            Invoke-External "npm" @("run", "check:toolchain") (Join-Path $RepoRoot "webUI") -Required | Out-Null
        }
        "Infrastructure" {
            Invoke-External "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "scripts\setup\Test-LocalBaseline.ps1"), "-InfrastructureOnly") $RepoRoot -Required | Out-Null
        }
        "Runtime" {
            Invoke-External "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "scripts\setup\Test-LocalBaseline.ps1"), "-Full") $RepoRoot -Required | Out-Null
        }
        "Full" {
            Invoke-External "dotnet" @("build", ".\NatureProtector.sln", "-c", "Release", "--no-restore", "--nologo", "-v", "minimal", "-m:1") $RepoRoot -Required | Out-Null
            Invoke-External "dotnet" @("test", ".\NatureProtector.sln", "-c", "Release", "--no-build", "--no-restore", "--filter", "Category!=Docker", "--logger", "trx", "--results-directory", ".\artifacts\test-results") $RepoRoot -Required | Out-Null
            Invoke-External "npm" @("run", "typecheck") (Join-Path $RepoRoot "webUI") -Required | Out-Null
            Invoke-External "npm" @("run", "lint") (Join-Path $RepoRoot "webUI") -Required | Out-Null
            Invoke-External "npm" @("run", "format:check") (Join-Path $RepoRoot "webUI") -Required | Out-Null
            Invoke-External "npm" @("test", "--", "--run") (Join-Path $RepoRoot "webUI") -Required | Out-Null
        }
        "Security" {
            $securityOutputRoot = Join-Path $RepoRoot "artifacts\validation\workspace-profiles\security"
            Invoke-External "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "scripts\ci\check-dotnet-audit.ps1"), "-OutputPath", (Join-Path $securityOutputRoot "dotnet-audit.txt")) $RepoRoot -Required | Out-Null
            Invoke-External "npm" @("run", "test:audit-script") (Join-Path $RepoRoot "webUI") -Required | Out-Null
            Invoke-External "npm" @("run", "audit:ci", "--", (Join-Path $securityOutputRoot "npm-audit.json")) (Join-Path $RepoRoot "webUI") -Required | Out-Null
            Invoke-External "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "scripts\ci\check-secret-canaries.ps1"), "-RepositoryRoot", $RepoRoot, "-NoGit") $RepoRoot -Required | Out-Null
            Invoke-External "dotnet" @("test", ".\tests\NatureProtector.Backoffice.Api.Tests\NatureProtector.Backoffice.Api.Tests.csproj", "-c", "Release", "--no-restore", "--filter", "FullyQualifiedName~JwtAuthenticationTests|FullyQualifiedName~AuthorizationMatrixTests|FullyQualifiedName~RuntimeEvidenceHttpSecurityTests", "--logger", "trx;LogFileName=workspace-security.trx", "--results-directory", ".\artifacts\validation\workspace-profiles\security\test-results") $RepoRoot -Required | Out-Null
        }
        "PerformanceSmoke" {
            Invoke-External "pwsh" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Join-Path $RepoRoot "scripts\performance\run-benchmarks.ps1"), "-Profile", "B0", "-Filter", "*SerializationBenchmarks.SerializeEnvelopeBatch*", "-OutputRoot", "artifacts\validation\workspace-profiles\performance-smoke", "-TimeoutSeconds", "180") $RepoRoot -Required | Out-Null
        }
    }
}

function Write-WorkspaceHelp {
    @"
NatureProtector workspace command

Usage:
  .\scripts\np.ps1 doctor
  .\scripts\np.ps1 init-local -Force
  .\scripts\np.ps1 up
  .\scripts\np.ps1 start
  .\scripts\np.ps1 health
  .\scripts\np.ps1 stop
  .\scripts\np.ps1 down

Compatibility:
  .\scripts\workspace.ps1 setup [-PlanOnly] [-NoDependencyRestore] [-NoPlaywrightInstall]
  .\scripts\workspace.ps1 up [-PlanOnly] [-StartRuntime] [-OpenBrowser]
  .\scripts\workspace.ps1 validate [-Profile Quick|Full|Infrastructure|Runtime|Security|PerformanceSmoke] [-PlanOnly]
  .\scripts\workspace.ps1 down [-PlanOnly] [-Force]
  .\scripts\workspace.ps1 reset -Confirm RESET_LOCAL_INFRA [-PlanOnly] [-Force]
  .\scripts\workspace.ps1 help

Guarantees:
  - Does not execute Git commands. It only checks whether a Git executable exists.
  - Does not create, edit, sanitize, copy, or delete .env or .env.example.
  - Destructive reset requires the literal confirmation token RESET_LOCAL_INFRA.
"@ | Write-Host
}

try {
    switch ($Command) {
        "setup" { Invoke-WorkspaceSetup }
        "up" { Invoke-WorkspaceUp }
        "validate" { Invoke-WorkspaceValidate }
        "down" { Invoke-WorkspaceDown }
        "reset" { Invoke-WorkspaceReset }
        "help" { Write-WorkspaceHelp }
    }

    Write-WorkspaceLine "OK" "workspace '$Command' completed."
}
catch {
    Write-WorkspaceLine "FAIL" $_.Exception.Message
    exit 1
}
