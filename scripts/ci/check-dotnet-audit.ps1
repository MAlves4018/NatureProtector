param(
    [string]$Solution = ".\NatureProtector.sln",
    [string]$OutputPath = ".\artifacts\dotnet-audit.txt"
)

$ErrorActionPreference = "Stop"

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory))
{
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$auditOutput = & dotnet list $Solution package --vulnerable --include-transitive 2>&1
$exitCode = $LASTEXITCODE

$auditOutput | Set-Content -Encoding utf8 -Path $OutputPath
$auditOutput | Write-Output

if ($exitCode -ne 0)
{
    exit $exitCode
}

$blocking = $auditOutput | Where-Object { $_ -match '\b(High|Critical)\b' }
if ($blocking)
{
    throw "NuGet audit found high or critical vulnerabilities. Full output was written to $OutputPath."
}

Write-Host "NuGet audit report written to $OutputPath"
