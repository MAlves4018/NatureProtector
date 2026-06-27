[CmdletBinding()]
param(
    [ValidateSet("staging")]
    [string]$Environment = "staging",

    [string]$StateBucket = $env:TF_STATE_BUCKET,

    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot = (
    Resolve-Path (
        Join-Path $PSScriptRoot "..\.."
    )
).Path

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path `
        (Split-Path -Parent $RepoRoot) `
        "NatureProtector-Standard-CD-Result-local\canonical-backend"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $RepoRoot $OutputDirectory
}

$OutputDirectory = [System.IO.Path]::GetFullPath(
    $OutputDirectory
)

if ([string]::IsNullOrWhiteSpace($StateBucket)) {
    throw (
        "Pass -StateBucket or define TF_STATE_BUCKET."
    )
}

if ($StateBucket -notmatch '^[a-z0-9][a-z0-9._-]{1,61}[a-z0-9]$') {
    throw "Invalid GCS bucket name: $StateBucket"
}

$CommonPath = Join-Path `
    $RepoRoot `
    "deploy\environments\common.json"

$EnvironmentPath = Join-Path `
    $RepoRoot `
    "deploy\environments\$Environment.json"

$Common = Get-Content `
    -Raw `
    -LiteralPath $CommonPath |
    ConvertFrom-Json

$EnvironmentConfig = Get-Content `
    -Raw `
    -LiteralPath $EnvironmentPath |
    ConvertFrom-Json

$Backend = $Common.terraform.backend

if ($Backend.type -ne "gcs") {
    throw "Only the gcs backend is supported."
}

if ($Backend.bucket_variable -ne "TF_STATE_BUCKET") {
    throw (
        "Canonical backend bucket variable must be TF_STATE_BUCKET."
    )
}

if ($EnvironmentConfig.environment -ne $Environment) {
    throw (
        "Environment configuration mismatch: " +
        "$($EnvironmentConfig.environment)"
    )
}

if (-not $EnvironmentConfig.deployable) {
    throw "Environment '$Environment' is not deployable."
}

$Prefixes = [ordered]@{
    "state-bootstrap" = $Backend.state_bootstrap_prefix
    "platform"        = $Backend.platform_prefix
    $Environment      = $EnvironmentConfig.terraform_state_prefix
}

$UniquePrefixes = @(
    $Prefixes.Values |
    Select-Object -Unique
)

if ($UniquePrefixes.Count -ne $Prefixes.Count) {
    throw "Terraform backend prefixes must be unique."
}

foreach ($Name in $Prefixes.Keys) {
    $Prefix = [string]$Prefixes[$Name]

    if ([string]::IsNullOrWhiteSpace($Prefix)) {
        throw "Backend prefix '$Name' is empty."
    }

    if (
        $Prefix.StartsWith("/") -or
        $Prefix.EndsWith("/") -or
        $Prefix.Contains("//") -or
        $Prefix.Contains("..")
    ) {
        throw "Unsafe backend prefix '$Prefix'."
    }
}

Remove-Item `
    -LiteralPath $OutputDirectory `
    -Recurse `
    -Force `
    -ErrorAction SilentlyContinue

New-Item `
    -ItemType Directory `
    -Path $OutputDirectory `
    -Force |
Out-Null

$GeneratedFiles = @()

foreach ($Name in $Prefixes.Keys) {
    $Prefix = [string]$Prefixes[$Name]
    $FileName = switch ($Name) {
        "state-bootstrap" {
            "state-bootstrap.gcs.tfbackend"
        }
        "platform" {
            "platform.gcs.tfbackend"
        }
        default {
            "$Name.gcs.tfbackend"
        }
    }
    $FilePath = Join-Path $OutputDirectory $FileName

    $Content = @(
        "bucket = `"$StateBucket`""
        "prefix = `"$Prefix`""
    ) -join [Environment]::NewLine

    Set-Content `
        -LiteralPath $FilePath `
        -Value ($Content + [Environment]::NewLine) `
        -Encoding utf8

    $GeneratedFiles += [ordered]@{
        name   = $Name
        path   = $FilePath
        bucket = $StateBucket
        prefix = $Prefix
    }
}

$Result = [ordered]@{
    operation                = "canonical-terraform-backend-files"
    status                   = "passed"
    environment              = $Environment
    backend_type             = "gcs"
    state_bucket             = $StateBucket
    cloud_mutation           = $false
    terraform_apply_executed = $false
    output_directory         = $OutputDirectory
    generated_files          = $GeneratedFiles
}

$ResultPath = Join-Path `
    $OutputDirectory `
    "operation-result.json"

$Result |
ConvertTo-Json -Depth 8 |
Set-Content `
    -LiteralPath $ResultPath `
    -Encoding utf8

$Result |
ConvertTo-Json -Depth 8
