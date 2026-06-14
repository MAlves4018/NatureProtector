<#
.SYNOPSIS
Prepares local Pipeline and Sim demo identities through the existing Backoffice API.

.DESCRIPTION
This script uses only the current user-plane API and existing roles. It does not
create roles, alter claims, write secrets to the repository, or change database
schema. Passwords must be supplied through parameters or environment variables.
#>

[CmdletBinding()]
param(
    [string]$ApiBaseUrl = "http://localhost:5254",
    [string]$AdminUsername = $env:NP_DEMO_ADMIN_USERNAME,
    [string]$AdminPassword = $env:NP_DEMO_ADMIN_PASSWORD,
    [string]$PipelineUsername = $env:NP_DEMO_PIPELINE_USERNAME,
    [string]$PipelinePassword = $env:NP_DEMO_PIPELINE_PASSWORD,
    [string]$SimUsername = $env:NP_DEMO_SIM_USERNAME,
    [string]$SimPassword = $env:NP_DEMO_SIM_PASSWORD,
    [switch]$DryRun,
    [switch]$ValidateJourneys
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

if ([string]::IsNullOrWhiteSpace($AdminUsername)) {
    $AdminUsername = "admin"
}

if ([string]::IsNullOrWhiteSpace($PipelineUsername)) {
    $PipelineUsername = "pipeline.local"
}

if ([string]::IsNullOrWhiteSpace($SimUsername)) {
    $SimUsername = "sim.local"
}

function Assert-SecretPresent {
    param(
        [string]$Name,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$Name is required. Set it by parameter or environment variable; do not commit demo passwords to the repository."
    }
}

function Invoke-ApiJson {
    param(
        [ValidateSet("GET", "POST", "PUT", "DELETE")]
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [string]$Token = $null,
        [int[]]$ExpectedStatusCodes = @(200)
    )

    $uri = "$ApiBaseUrl$Path"
    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers.Authorization = "Bearer $Token"
    }

    try {
        $parameters = @{
            Method = $Method
            Uri = $uri
            Headers = $headers
            UseBasicParsing = $true
            TimeoutSec = 30
            ErrorAction = "Stop"
        }

        if ($null -ne $Body) {
            $parameters.ContentType = "application/json"
            $parameters.Body = ($Body | ConvertTo-Json -Depth 8)
        }

        $response = Invoke-WebRequest @parameters
        $statusCode = [int]$response.StatusCode
        if ($ExpectedStatusCodes -notcontains $statusCode) {
            throw "$Method $uri returned HTTP $statusCode; expected $($ExpectedStatusCodes -join ', ')."
        }

        $content = [string]$response.Content
        $json = if ([string]::IsNullOrWhiteSpace($content)) { $null } else { $content | ConvertFrom-Json }
        return [pscustomobject]@{
            StatusCode = $statusCode
            Json = $json
            Raw = $content
        }
    }
    catch {
        $statusCode = $null
        $content = ""
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                if ($stream) {
                    $reader = [System.IO.StreamReader]::new($stream)
                    $content = $reader.ReadToEnd()
                }
            }
            catch {
                $content = ""
            }
        }
        elseif ($_.Exception.Message -match '\((\d{3})\)') {
            $statusCode = [int]$Matches[1]
        }

        if ($statusCode -and ($ExpectedStatusCodes -contains $statusCode)) {
            $json = if ([string]::IsNullOrWhiteSpace($content)) { $null } else { $content | ConvertFrom-Json }
            return [pscustomobject]@{
                StatusCode = $statusCode
                Json = $json
                Raw = $content
            }
        }

        throw
    }
}

function Login-User {
    param(
        [string]$Username,
        [string]$Password
    )

    $response = Invoke-ApiJson `
        -Method "POST" `
        -Path "/api/users-roles/login" `
        -Body @{ usernameOrEmail = $Username; password = $Password } `
        -ExpectedStatusCodes @(200)

    return $response.Json
}

function New-DemoUserRequest {
    param(
        [string]$Username,
        [string]$Password,
        [string]$Role
    )

    return @{
        username = $Username
        password = $Password
        email = "$Username@example.local"
        organization = "NatureProtector local demo"
        roles = @($Role)
    }
}

function Find-ExistingUserInRole {
    param(
        [string]$Username,
        [int]$RoleId,
        [string]$AdminToken
    )

    $response = Invoke-ApiJson `
        -Method "GET" `
        -Path "/api/users-roles/roles/$RoleId/users" `
        -Token $AdminToken `
        -ExpectedStatusCodes @(200)

    $users = @($response.Json)
    return $users | Where-Object { $_.username -eq $Username } | Select-Object -First 1
}

function Ensure-DemoUser {
    param(
        [string]$Username,
        [string]$Password,
        [string]$Role,
        [int]$RoleId,
        [string]$AdminToken
    )

    if ($DryRun) {
        Write-Host "[DRY-RUN] Would ensure user '$Username' with role '$Role'."
        return
    }

    $request = New-DemoUserRequest -Username $Username -Password $Password -Role $Role
    $created = Invoke-ApiJson `
        -Method "POST" `
        -Path "/api/users-roles/users" `
        -Body $request `
        -Token $AdminToken `
        -ExpectedStatusCodes @(200, 409)

    if ($created.StatusCode -eq 200) {
        Write-Host "[OK] Created '$Username' with role '$Role'."
        return
    }

    $login = $null
    try {
        $login = Login-User -Username $Username -Password $Password
    }
    catch {
        $existing = Find-ExistingUserInRole -Username $Username -RoleId $RoleId -AdminToken $AdminToken
        if ($null -eq $existing) {
            throw "User '$Username' already exists, but the supplied password did not work and the user was not found in role '$Role'. Use the admin UI/API to reconcile it."
        }

        [void](Invoke-ApiJson `
            -Method "PUT" `
            -Path "/api/users-roles/users/$($existing.id)" `
            -Body $request `
            -Token $AdminToken `
            -ExpectedStatusCodes @(200))

        Write-Host "[OK] Updated existing '$Username' and enforced role '$Role'."
        return
    }

    [void](Invoke-ApiJson `
        -Method "PUT" `
        -Path "/api/users-roles/users/$($login.userId)" `
        -Body $request `
        -Token $AdminToken `
        -ExpectedStatusCodes @(200))

    Write-Host "[OK] Existing '$Username' login succeeded; role '$Role' enforced."
}

function Validate-PipelineJourney {
    param(
        [string]$Username,
        [string]$Password
    )

    $login = Login-User -Username $Username -Password $Password
    [void](Invoke-ApiJson `
        -Method "GET" `
        -Path "/api/control/runtime/summary?areaCode=proenca-a-nova&recentMinutes=30" `
        -Token $login.token `
        -ExpectedStatusCodes @(200))

    [void](Invoke-ApiJson `
        -Method "POST" `
        -Path "/api/control/runtime/runs" `
        -Token $login.token `
        -Body (New-MinimalRunRequest "pre-external-pipeline-denied") `
        -ExpectedStatusCodes @(403))

    Write-Host "[OK] Pipeline journey: login ok, runtime read allowed, runtime start forbidden."
}

function New-MinimalRunRequest {
    param([string]$RunLabel)

    return @{
        areaCode = "proenca-a-nova"
        scenarioCode = "scenario_b"
        sensorCount = 1
        numberOfCycles = 1
        intervalSeconds = 1
        seed = 4142
        degradationProfile = "none"
        degradationProfiles = @("none")
        waitForCompletion = $true
        timeoutSeconds = 120
        allowParallelRun = $false
        runLabel = $RunLabel
    }
}

function Validate-SimJourney {
    param(
        [string]$Username,
        [string]$Password
    )

    $login = Login-User -Username $Username -Password $Password
    [void](Invoke-ApiJson `
        -Method "GET" `
        -Path "/api/control/areas/proenca-a-nova/scenarios" `
        -Token $login.token `
        -ExpectedStatusCodes @(200))

    $runResponse = Invoke-ApiJson `
        -Method "POST" `
        -Path "/api/control/runtime/runs" `
        -Token $login.token `
        -Body (New-MinimalRunRequest "pre-external-sim-identity-smoke") `
        -ExpectedStatusCodes @(200)

    $runId = $runResponse.Json.run.id
    if ([string]::IsNullOrWhiteSpace([string]$runId)) {
        throw "Sim journey accepted the run request but did not return a run id."
    }

    Write-Host "[OK] Sim journey: login ok, scenario selection allowed, runtime start allowed, run id $runId."
}

Assert-SecretPresent "AdminPassword/NP_DEMO_ADMIN_PASSWORD" $AdminPassword
Assert-SecretPresent "PipelinePassword/NP_DEMO_PIPELINE_PASSWORD" $PipelinePassword
Assert-SecretPresent "SimPassword/NP_DEMO_SIM_PASSWORD" $SimPassword

if ($DryRun) {
    Write-Host "[DRY-RUN] API base URL: $ApiBaseUrl"
    Write-Host "[DRY-RUN] Admin user: $AdminUsername"
    Write-Host "[DRY-RUN] Pipeline user: $PipelineUsername"
    Write-Host "[DRY-RUN] Sim user: $SimUsername"
    Write-Host "[DRY-RUN] No API writes or runtime runs will be executed."
    exit 0
}

$adminLogin = Login-User -Username $AdminUsername -Password $AdminPassword
if (-not ($adminLogin.roles -contains "Admin")) {
    throw "Admin identity '$AdminUsername' logged in but does not have the Admin role."
}

Ensure-DemoUser -Username $PipelineUsername -Password $PipelinePassword -Role "Pipeline" -RoleId 3 -AdminToken $adminLogin.token
Ensure-DemoUser -Username $SimUsername -Password $SimPassword -Role "Sim" -RoleId 2 -AdminToken $adminLogin.token

if ($ValidateJourneys) {
    Validate-PipelineJourney -Username $PipelineUsername -Password $PipelinePassword
    Validate-SimJourney -Username $SimUsername -Password $SimPassword
}

Write-Host "[OK] Local demo identities are prepared."
