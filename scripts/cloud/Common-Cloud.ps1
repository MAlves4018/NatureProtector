Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-CloudStep {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Get-CloudSetting {
    [CmdletBinding()]
    param(
        [AllowEmptyString()][string]$ParameterValue,
        [Parameter(Mandatory = $true)][string]$EnvironmentName,
        [AllowEmptyString()][string]$DefaultValue,
        [switch]$Required
    )

    if (-not [string]::IsNullOrWhiteSpace($ParameterValue)) {
        return $ParameterValue.Trim()
    }

    $value = [Environment]::GetEnvironmentVariable($EnvironmentName, 'Process')
    if ([string]::IsNullOrWhiteSpace($value)) {
        $value = [Environment]::GetEnvironmentVariable($EnvironmentName, 'User')
    }

    if (-not [string]::IsNullOrWhiteSpace($value)) {
        return $value.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($DefaultValue)) {
        return $DefaultValue.Trim()
    }

    if ($Required) {
        throw "A configuração '$EnvironmentName' é obrigatória. Defina-a no ambiente ou passe o parâmetro correspondente."
    }

    return $null
}

function Assert-CloudProjectId {
    param([Parameter(Mandatory = $true)][string]$ProjectId)

    if ($ProjectId -notmatch '^[a-z][a-z0-9-]{4,28}[a-z0-9]$') {
        throw "Project ID inválido: '$ProjectId'."
    }
}

function Assert-BillingAccountId {
    param([Parameter(Mandatory = $true)][string]$BillingAccountId)

    if ($BillingAccountId -notmatch '^[0-9A-Fa-f]{6}-[0-9A-Fa-f]{6}-[0-9A-Fa-f]{6}$') {
        throw "Billing Account ID inválido. Formato esperado: XXXXXX-XXXXXX-XXXXXX."
    }
}

function Assert-CloudRegion {
    param([Parameter(Mandatory = $true)][string]$Region)

    if ($Region -notmatch '^[a-z]+-[a-z]+[0-9]+$') {
        throw "Região Google Cloud inválida: '$Region'."
    }
}

function Test-CommandAvailable {
    param([Parameter(Mandatory = $true)][string]$Name)
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function ConvertTo-CloudCommandText {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return ''
    }

    $text = [string](($Value | ForEach-Object {
        if ($null -eq $_) {
            ''
        }
        else {
            [string]$_
        }
    }) -join "`n")

    return $text.Trim()
}

function Split-CloudCommandLines {
    param([AllowNull()][object]$Value)

    $text = ConvertTo-CloudCommandText $Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        return @()
    }

    return @(
        $text -split "`r?`n" |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
}

function Invoke-CapturedCommand {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Command,
        [string[]]$Arguments = @(),
        [switch]$AllowFailure
    )

    $stderrFile = [System.IO.Path]::GetTempFileName()
    try {
        $output = $null
        $commandError = ''
        $global:LASTEXITCODE = 0

        try {
            $output = & $Command @Arguments 2> $stderrFile
            $exitCode = if ($null -ne $LASTEXITCODE) { [int]$LASTEXITCODE } else { 0 }
        }
        catch {
            $exitCode = 127
            $commandError = $_.Exception.Message
        }

        $stderr = $null
        if (Test-Path $stderrFile) {
            $stderr = (Get-Content -Raw -ErrorAction SilentlyContinue $stderrFile)
        }

        $errorText = ConvertTo-CloudCommandText $stderr
        if (-not [string]::IsNullOrWhiteSpace($commandError)) {
            $errorText = ConvertTo-CloudCommandText @($errorText, $commandError)
        }

        $result = [pscustomobject]@{
            ExitCode = $exitCode
            Output   = ConvertTo-CloudCommandText $output
            Error    = $errorText
        }

        if (($exitCode -ne 0) -and (-not $AllowFailure)) {
            $details = if (-not [string]::IsNullOrWhiteSpace($result.Error)) {
                $result.Error
            }
            else {
                $result.Output
            }

            throw "O comando falhou ($exitCode): $Command $($Arguments -join ' ')`n$details"
        }

        return $result
    }
    finally {
        Remove-Item -Force -ErrorAction SilentlyContinue $stderrFile
    }
}

function Invoke-GCloudText {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [switch]$AllowFailure
    )

    $result = Invoke-CapturedCommand -Command 'gcloud' -Arguments $Arguments -AllowFailure:$AllowFailure
    if (($result.ExitCode -ne 0) -and (-not $AllowFailure)) {
        throw "gcloud falhou."
    }

    return $result
}

function Invoke-GCloudJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [switch]$AllowFailure
    )

    $jsonArguments = @($Arguments)
    if (-not ($jsonArguments | Where-Object { $_ -like '--format=*' })) {
        $jsonArguments += '--format=json'
    }

    $result = Invoke-GCloudText -Arguments $jsonArguments -AllowFailure:$AllowFailure
    if ($result.ExitCode -ne 0) {
        return [pscustomobject]@{
            Succeeded = $false
            Data      = $null
            Error     = $result.Error
            Output    = $result.Output
        }
    }

    if ([string]::IsNullOrWhiteSpace($result.Output)) {
        $data = $null
    }
    else {
        try {
            $data = $result.Output | ConvertFrom-Json
        }
        catch {
            throw "Não foi possível interpretar JSON devolvido por gcloud.`n$($result.Output)"
        }
    }

    return [pscustomobject]@{
        Succeeded = $true
        Data      = $data
        Error     = $result.Error
        Output    = $result.Output
    }
}

function Invoke-InteractiveGCloud {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    & gcloud @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "O comando interativo gcloud falhou: gcloud $($Arguments -join ' ')"
    }
}

function Get-RequiredCloudApis {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Lista de APIs não encontrada: $Path"
    }

    return @(
        Get-Content -LiteralPath $Path |
            ForEach-Object { $_.Trim() } |
            Where-Object {
                (-not [string]::IsNullOrWhiteSpace($_)) -and
                (-not $_.StartsWith('#'))
            } |
            Sort-Object -Unique
    )
}

function Get-MaskedBillingAccountId {
    param([AllowEmptyString()][string]$BillingAccountId)

    if ([string]::IsNullOrWhiteSpace($BillingAccountId)) {
        return $null
    }

    if ($BillingAccountId.Length -lt 6) {
        return '***'
    }

    return "******-******-$($BillingAccountId.Substring($BillingAccountId.Length - 6))"
}

function Write-CloudEvidence {
    param(
        [Parameter(Mandatory = $true)][object]$Data,
        [Parameter(Mandatory = $true)][string]$Directory,
        [Parameter(Mandatory = $true)][string]$FilePrefix
    )

    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
    $path = Join-Path $Directory "$FilePrefix-$timestamp.json"
    $Data | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $path -Encoding UTF8
    return (Resolve-Path $path).Path
}

function Get-CloudTimestamp {
    return (Get-Date).ToUniversalTime().ToString('o')
}
