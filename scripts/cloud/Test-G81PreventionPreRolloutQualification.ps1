[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string]$SourceRoot,
    [Parameter(Mandatory)][string]$PlatformProjectId,
    [Parameter(Mandatory)][string]$StagingProjectId,
    [Parameter(Mandatory)][string]$Region,
    [Parameter(Mandatory)][string]$ClusterName,
    [Parameter(Mandatory)][string]$Target,
    [Parameter(Mandatory)][string]$Namespace,
    [Parameter(Mandatory)][string]$CloudSqlPrivateIp,
    [Parameter(Mandatory)][string]$RabbitMqTlsServerName,
    [Parameter(Mandatory)][string]$RabbitMqTlsCertificateVersion,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$RabbitMqTlsCertificateSecret = "np-staging-rabbitmq-tls-certificate",
    [string]$CloudSqlInstance = "np-staging-postgres"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($Region -ne "europe-southwest1") { throw "Unexpected region '$Region'." }
if ($Namespace -ne "natureprotector-staging") { throw "Unexpected namespace '$Namespace'." }
if ($Target -ne "np-gke-staging") { throw "Unexpected Cloud Deploy target '$Target'." }
if ($CloudSqlInstance -ne "np-staging-postgres") { throw "Unexpected staging Cloud SQL instance '$CloudSqlInstance'." }

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null

function Get-JsonProperty {
    param(
        [Parameter(Mandatory)]$Object,
        [Parameter(Mandatory)][string]$Name
    )

    if ($null -eq $Object) { return $null }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name)) { return $Object[$Name] }
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property) { return $property.Value }
    return $null
}

function Invoke-RecordedCommand {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$OutputPath,
        [switch]$AllowFailure
    )

    $output = & $FilePath @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output | Set-Content -Encoding utf8 -LiteralPath $OutputPath
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "$FilePath failed with exit code $exitCode. See $OutputPath."
    }

    return [ordered]@{
        exit_code = $exitCode
        output = ($output -join "`n")
    }
}

function Add-DeployParameterValues {
    param([Parameter(Mandatory)]$Node)

    if ($null -eq $Node) { return }

    if ($Node -is [System.Collections.IEnumerable] -and -not ($Node -is [string]) -and -not ($Node -is [System.Collections.IDictionary])) {
        foreach ($item in $Node) { Add-DeployParameterValues -Node $item }
        return
    }

    if ($Node -is [System.Collections.IDictionary]) {
        if ($Node.Contains("values")) {
            Add-DeployParameterValues -Node $Node["values"]
            return
        }

        foreach ($entry in $Node.GetEnumerator()) {
            if ([string]$entry.Key -eq "matchTargetLabels") { continue }
            $script:deployParameterMap[[string]$entry.Key] = [string]$entry.Value
        }
        return
    }

    $valuesProperty = $Node.PSObject.Properties["values"]
    if ($null -ne $valuesProperty) {
        Add-DeployParameterValues -Node $valuesProperty.Value
        return
    }

    foreach ($property in $Node.PSObject.Properties) {
        if ($property.Name -eq "matchTargetLabels") { continue }
        $script:deployParameterMap[[string]$property.Name] = [string]$property.Value
    }
}

function Get-TargetDeployParameters {
    $targetPath = Join-Path $EvidenceDirectory "cloud-deploy-target.json"
    $targetResult = Invoke-RecordedCommand -FilePath "gcloud" -Arguments @(
        "deploy", "targets", "describe", $Target,
        "--project=$PlatformProjectId", "--region=$Region", "--format=json"
    ) -OutputPath $targetPath
    $targetJson = $targetResult.output | ConvertFrom-Json
    $targetObject = Get-JsonProperty -Object $targetJson -Name "Target"
    if ($null -eq $targetObject) { $targetObject = $targetJson }
    $deployParameters = Get-JsonProperty -Object $targetObject -Name "deployParameters"
    if ($null -eq $deployParameters) { throw "Cloud Deploy target '$Target' has no deployParameters." }

    $script:deployParameterMap = [ordered]@{}
    Add-DeployParameterValues -Node $deployParameters
    $script:deployParameterMap | ConvertTo-Json -Depth 8 |
        Set-Content -Encoding utf8 -LiteralPath (Join-Path $EvidenceDirectory "deploy-parameters.json")
    return $script:deployParameterMap
}

function Assert-DeployParameter {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary]$Parameters,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Expected
    )

    if (-not $Parameters.Contains($Name)) {
        Write-Host "PREVENTION_DEPENDENCY_CONTRACT=FAIL"
        throw "Cloud Deploy target '$Target' is missing deploy parameter '$Name'."
    }

    $actual = [string]$Parameters[$Name]
    if ($actual -ne $Expected) {
        Write-Host "PREVENTION_DEPENDENCY_CONTRACT=FAIL"
        throw "Cloud Deploy target '$Target' deploy parameter '$Name' is '$actual', expected '$Expected'."
    }
}

function Get-ManifestImageReference {
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary]$Manifest,
        [Parameter(Mandatory)][string]$Name
    )

    $images = $Manifest["images"]
    if ($null -eq $images -or -not $images.Contains($Name)) {
        throw "Release manifest is missing image '$Name'."
    }

    $image = $images[$Name]
    if ($null -eq $image -or -not $image.Contains("reference")) {
        throw "Release manifest image '$Name' is missing reference."
    }

    return [string]$image["reference"]
}

function Get-SecretKeys {
    param([Parameter(Mandatory)][string]$Name)

    $json = & kubectl -n $Namespace get secret $Name -o json 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Required secret '$Name' is missing in namespace '$Namespace'." }
    $secret = ($json -join "`n") | ConvertFrom-Json
    $data = Get-JsonProperty -Object $secret -Name "data"
    if ($null -eq $data) { return @() }
    return @($data.PSObject.Properties.Name | Sort-Object)
}

function Assert-SecretKey {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Key
    )

    $keys = Get-SecretKeys -Name $Name
    $script:secretKeyEvidence[$Name] = @($keys)
    if (-not ($keys -contains $Key)) {
        throw "Required secret '$Name' is missing key '$Key'."
    }
}

function Set-RabbitMqTopologyUriIfPresent {
    $secretName = "natureprotector-rabbitmq-default-user"
    $secretPresence = (& kubectl -n $Namespace get secret $secretName -o name --ignore-not-found 2>&1) -join ""
    if ($LASTEXITCODE -ne 0) { throw "Unable to inspect secret '$secretName'." }
    if ([string]::IsNullOrWhiteSpace($secretPresence)) {
        "secret $secretName is not present before rollout; Cloud Deploy verifier reconciles it after RabbitMQ deployment." |
            Set-Content -Encoding utf8 -LiteralPath (Join-Path $EvidenceDirectory "rabbitmq-topology-uri-skipped.txt")
        return $false
    }

    $rabbitMqManagementUri = "http://natureprotector-rabbitmq.natureprotector-staging.svc:15672"
    $rabbitMqManagementUriBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($rabbitMqManagementUri))
    Invoke-RecordedCommand -FilePath "kubectl" -Arguments @(
        "patch", "secret", $secretName,
        "--namespace=$Namespace",
        "--request-timeout=30s",
        "--type=merge",
        "-p", "{`"data`":{`"uri`":`"$rabbitMqManagementUriBase64`"}}"
    ) -OutputPath (Join-Path $EvidenceDirectory "rabbitmq-topology-uri-patch.txt") | Out-Null
    return $true
}

function Assert-RabbitMqCertificateSan {
    param([Parameter(Mandatory)][string[]]$ExpectedDnsNames)

    $certificatePem = (& gcloud secrets versions access $RabbitMqTlsCertificateVersion --secret=$RabbitMqTlsCertificateSecret --project=$StagingProjectId 2>&1) -join "`n"
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($certificatePem)) {
        throw "Unable to read public RabbitMQ server certificate version '$RabbitMqTlsCertificateVersion' from Secret Manager."
    }
    $certificatePem = (($certificatePem -split "\r?\n") | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join "`n"

    $temporaryCertificate = Join-Path ([System.IO.Path]::GetTempPath()) "np-rabbitmq-server-$PID.crt"
    try {
        [System.IO.File]::WriteAllText($temporaryCertificate, $certificatePem, [System.Text.Encoding]::ASCII)
        $sanPath = Join-Path $EvidenceDirectory "rabbitmq-server-cert-san.txt"
        Invoke-RecordedCommand -FilePath "openssl" -Arguments @(
            "x509", "-in", $temporaryCertificate, "-noout", "-ext", "subjectAltName"
        ) -OutputPath $sanPath | Out-Null
        $sanText = Get-Content -Raw -LiteralPath $sanPath
        foreach ($dnsName in $ExpectedDnsNames) {
            if ($sanText -notmatch [regex]::Escape("DNS:$dnsName")) {
                throw "RabbitMQ server certificate SAN is missing DNS:$dnsName."
            }
        }
    }
    finally {
        Remove-Item -Force -ErrorAction SilentlyContinue -LiteralPath $temporaryCertificate
    }
}

function Assert-CloudSqlPrivateIp {
    $cloudSqlPath = Join-Path $EvidenceDirectory "cloud-sql-instance.json"
    $result = Invoke-RecordedCommand -FilePath "gcloud" -Arguments @(
        "sql", "instances", "describe", $CloudSqlInstance,
        "--project=$StagingProjectId", "--format=json"
    ) -OutputPath $cloudSqlPath
    $instance = $result.output | ConvertFrom-Json
    $privateIps = @(
        $instance.ipAddresses |
            Where-Object { [string]$_.type -eq "PRIVATE" } |
            ForEach-Object { [string]$_.ipAddress }
    )
    if (-not ($privateIps -contains $CloudSqlPrivateIp)) {
        throw "Cloud SQL instance '$CloudSqlInstance' does not expose expected private IP '$CloudSqlPrivateIp'."
    }
}

$manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json -AsHashtable
$deployParameters = Get-TargetDeployParameters
Assert-DeployParameter -Parameters $deployParameters -Name "cloud_sql_private_ip" -Expected $CloudSqlPrivateIp
Assert-DeployParameter -Parameters $deployParameters -Name "cloud_sql_private_cidr" -Expected "$CloudSqlPrivateIp/32"
Assert-DeployParameter -Parameters $deployParameters -Name "rabbitmq_tls_server_name" -Expected $RabbitMqTlsServerName
Write-Host "PREVENTION_DEPENDENCY_CONTRACT=PASS"

$skaffoldFile = Join-Path $SourceRoot "cloud-deploy/g8-1/prevention/skaffold.yaml"
$skaffoldCommand = Get-Command skaffold -ErrorAction SilentlyContinue
if ($null -ne $skaffoldCommand) {
    Invoke-RecordedCommand -FilePath $skaffoldCommand.Source -Arguments @(
        "diagnose", "--filename", $skaffoldFile
    ) -OutputPath (Join-Path $EvidenceDirectory "skaffold-diagnose.txt") | Out-Null
    Write-Host "PREVENTION_SKAFFOLD_DIAGNOSE=PASS"
}
else {
    "skaffold CLI unavailable on runner; kubectl kustomize and server dry-run remain enforced." |
        Set-Content -Encoding utf8 -LiteralPath (Join-Path $EvidenceDirectory "skaffold-diagnose-skipped.txt")
    Write-Host "PREVENTION_SKAFFOLD_DIAGNOSE=SKIPPED_UNAVAILABLE"
}

$overlayPath = Join-Path $SourceRoot "kubernetes/g8-1/overlays/staging"
$rawRenderPath = Join-Path $EvidenceDirectory "prevention-render.raw.yaml"
$renderResult = Invoke-RecordedCommand -FilePath "kubectl" -Arguments @(
    "kustomize", $overlayPath
) -OutputPath $rawRenderPath
$rendered = [string]$renderResult.output

$renderReplacements = [ordered]@{
    '${cloud_sql_private_ip}' = $CloudSqlPrivateIp
    'missing-cloud-sql-private-ip' = $CloudSqlPrivateIp
    '${cloud_sql_private_cidr}' = "$CloudSqlPrivateIp/32"
    '10.255.255.255/32 # from-param: ${cloud_sql_private_cidr}' = "$CloudSqlPrivateIp/32 # from-param: `${cloud_sql_private_cidr}"
    'PREVENTION_IMAGE_BY_DIGEST' = (Get-ManifestImageReference -Manifest $manifest -Name "prevention")
    'RABBITMQ_IMAGE_BY_DIGEST' = (Get-ManifestImageReference -Manifest $manifest -Name "rabbitmq")
    'OTEL_IMAGE_BY_DIGEST' = (Get-ManifestImageReference -Manifest $manifest -Name "otel-collector")
}
foreach ($name in @(
    "runtime_subnet_cidr",
    "prevention_gsa",
    "otel_gsa",
    "secret_sync_gsa",
    "otel_load_balancer_ip",
    "rabbitmq_load_balancer_ip",
    "rabbitmq_tls_server_name"
)) {
    if ($deployParameters.Contains($name)) {
        $renderReplacements['${' + $name + '}'] = [string]$deployParameters[$name]
    }
}

$fallbackDeployParameterMarkers = [ordered]@{
    "prevention_gsa" = "missing-prevention-gsa"
    "otel_gsa" = "missing-otel-gsa"
    "secret_sync_gsa" = "missing-secret-sync-gsa"
    "rabbitmq_tls_server_name" = "missing-rabbitmq-server-name"
}
foreach ($entry in $fallbackDeployParameterMarkers.GetEnumerator()) {
    if ($deployParameters.Contains([string]$entry.Key)) {
        $renderReplacements[[string]$entry.Value] = [string]$deployParameters[[string]$entry.Key]
    }
}
foreach ($entry in @{
    "runtime_subnet_cidr" = "10.255.255.255/32 # from-param: `${runtime_subnet_cidr}"
    "otel_load_balancer_ip" = "127.0.0.1 # from-param: `${otel_load_balancer_ip}"
    "rabbitmq_load_balancer_ip" = "127.0.0.1 # from-param: `${rabbitmq_load_balancer_ip}"
}.GetEnumerator()) {
    if ($deployParameters.Contains([string]$entry.Key)) {
        $renderReplacements[[string]$entry.Value] = "$($deployParameters[[string]$entry.Key]) # from-param: `${$($entry.Key)}"
    }
}

foreach ($entry in $renderReplacements.GetEnumerator()) {
    $rendered = $rendered.Replace([string]$entry.Key, [string]$entry.Value)
}

$prequalifiedRenderPath = Join-Path $EvidenceDirectory "prevention-render.prequalified.yaml"
$rendered | Set-Content -Encoding utf8 -LiteralPath $prequalifiedRenderPath

$unresolvedPatterns = @(
    "missing-",
    "MISSING_",
    "PREVENTION_IMAGE_BY_DIGEST",
    "RABBITMQ_IMAGE_BY_DIGEST",
    "OTEL_IMAGE_BY_DIGEST",
    "CLOUDSDK_IMAGE_BY_DIGEST",
    "CLOUD_SQL_CA_VERSION",
    "RABBITMQ_CA_VERSION",
    "RABBITMQ_TLS_CERTIFICATE_VERSION",
    "RABBITMQ_TLS_PRIVATE_KEY_VERSION",
    "/versions/latest",
    '${'
)
foreach ($pattern in $unresolvedPatterns) {
    if ($rendered.Contains($pattern)) {
        throw "Rendered Prevention manifest still contains unresolved marker '$pattern'."
    }
}
Write-Host "PREVENTION_RENDER_VALIDATION=PASS"

# kubectl apply --server-side --dry-run=server is the schema/admission gate.
Invoke-RecordedCommand -FilePath "kubectl" -Arguments @(
    "apply", "--server-side", "--dry-run=server", "-f", $prequalifiedRenderPath, "-n", $Namespace
) -OutputPath (Join-Path $EvidenceDirectory "prevention-server-dry-run.txt") | Out-Null
Write-Host "PREVENTION_SERVER_DRY_RUN=PASS"

Invoke-RecordedCommand -FilePath "kubectl" -Arguments @(
    "get", "namespace", $Namespace, "-o", "name"
) -OutputPath (Join-Path $EvidenceDirectory "namespace-presence.txt") | Out-Null

$script:secretKeyEvidence = [ordered]@{}
Assert-SecretKey -Name "np-runtime-secrets" -Key "postgres-app-password"
Assert-SecretKey -Name "np-runtime-secrets" -Key "rabbitmq-username"
Assert-SecretKey -Name "np-runtime-secrets" -Key "rabbitmq-password"
Assert-SecretKey -Name "np-rabbitmq-app-credentials" -Key "username"
Assert-SecretKey -Name "np-rabbitmq-app-credentials" -Key "password"
Assert-SecretKey -Name "np-rabbitmq-ca" -Key "ca.crt"
Assert-SecretKey -Name "np-rabbitmq-server-tls" -Key "tls.crt"
Assert-SecretKey -Name "np-rabbitmq-server-tls" -Key "tls.key"
Assert-SecretKey -Name "np-cloud-sql-ca" -Key "server-ca.pem"
if (Set-RabbitMqTopologyUriIfPresent) {
    Assert-SecretKey -Name "natureprotector-rabbitmq-default-user" -Key "uri"
}
$script:secretKeyEvidence | ConvertTo-Json -Depth 4 |
    Set-Content -Encoding utf8 -LiteralPath (Join-Path $EvidenceDirectory "secret-key-presence.json")

Assert-RabbitMqCertificateSan -ExpectedDnsNames @(
    "rabbitmq.staging.natureprotector.internal",
    "natureprotector-rabbitmq.natureprotector-staging.svc.cluster.local"
)
Assert-CloudSqlPrivateIp

[ordered]@{
    schema_version = 1
    environment = "staging"
    target = $Target
    namespace = $Namespace
    cloud_sql_private_ip = $CloudSqlPrivateIp
    rabbitmq_tls_server_name = $RabbitMqTlsServerName
    render_validation = "PASS"
    server_dry_run = "PASS"
    dependency_contract = "PASS"
    production_authorized = $false
} | ConvertTo-Json -Depth 5 |
    Set-Content -Encoding utf8 -LiteralPath (Join-Path $EvidenceDirectory "prevention-pre-rollout-qualification.json")

Write-Host "PREVENTION_PRE_ROLLOUT_QUALIFICATION=PASS"
