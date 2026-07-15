[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Inventory', 'Plan', 'Protect', 'RetireLegacyPolicy', 'Unbind', 'Verify', 'Rollback')]
    [string]$Action = 'Inventory',

    [Parameter(Mandatory = $true)]
    [uri]$ManagementBaseUri,

    [pscredential]$Credential,
    [string]$CertificateAuthorityPath,
    [switch]$AllowInsecureHttp,
    [string]$VirtualHost = '/',
    [string]$ExchangeName = 'np.events',
    [string]$PrimaryQueueName = 'np.ingestion.readings',
    [string]$RawQueueName = 'np.observability.raw',
    [string]$RoutingKey = 'simulation.reading.produced',
    [string]$PrimaryPolicyName = 'natureprotector-primary-work-queue',
    [string]$RawProtectionPolicyName = 'natureprotector-raw-migration-protection',
    [string]$LegacyBroadPolicyName = 'natureprotector-quorum',
    [long]$MessageTtlMilliseconds,
    [long]$MaxLengthBytes,
    [ValidateRange(1, 255)]
    [int]$RawPolicyPriority = 90,
    [string]$EvidenceDirectory = 'artifacts/operational-audit/rabbitmq-health-phase3f',
    [switch]$Apply,
    [string]$Confirmation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.Net.Http

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$resolvedEvidenceRoot = if ([System.IO.Path]::IsPathRooted($EvidenceDirectory)) {
    [System.IO.Path]::GetFullPath($EvidenceDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $EvidenceDirectory))
}
$timestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$runDirectory = Join-Path $resolvedEvidenceRoot "$timestamp-$($Action.ToLowerInvariant())"
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

function Get-RequiredCredential {
    param([pscredential]$ProvidedCredential)

    if ($null -ne $ProvidedCredential) {
        return $ProvidedCredential
    }

    $userName = [Environment]::GetEnvironmentVariable('RABBITMQ_MANAGEMENT_USERNAME')
    $password = [Environment]::GetEnvironmentVariable('RABBITMQ_MANAGEMENT_PASSWORD')
    if ([string]::IsNullOrWhiteSpace($userName) -or [string]::IsNullOrWhiteSpace($password)) {
        throw 'Provide -Credential or set RABBITMQ_MANAGEMENT_USERNAME and RABBITMQ_MANAGEMENT_PASSWORD.'
    }

    return [pscredential]::new(
        $userName,
        (ConvertTo-SecureString $password -AsPlainText -Force))
}

function Import-PrivateCertificateAuthority {
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolved = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
    try {
        return [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPemFile($resolved)
    }
    catch {
        try {
            return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($resolved)
        }
        catch {
            throw "RabbitMQ Management CA '$resolved' could not be loaded: $($_.Exception.Message)"
        }
    }
}

function New-RabbitMqManagementClient {
    param(
        [Parameter(Mandatory = $true)][uri]$BaseUri,
        [Parameter(Mandatory = $true)][pscredential]$ApiCredential,
        [string]$CaPath,
        [switch]$PermitHttp
    )

    if (-not $BaseUri.IsAbsoluteUri) {
        throw 'ManagementBaseUri must be absolute.'
    }
    if ($BaseUri.Scheme -notin @('http', 'https')) {
        throw "Unsupported RabbitMQ Management scheme '$($BaseUri.Scheme)'."
    }
    if ($BaseUri.Scheme -eq 'http' -and -not $PermitHttp) {
        throw 'HTTP RabbitMQ Management access requires -AllowInsecureHttp. Prefer HTTPS with -CertificateAuthorityPath.'
    }
    if ($BaseUri.Scheme -eq 'https' -and [string]::IsNullOrWhiteSpace($CaPath)) {
        throw 'HTTPS RabbitMQ Management access requires -CertificateAuthorityPath.'
    }

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false

    if ($BaseUri.Scheme -eq 'https') {
        $trustedRoot = Import-PrivateCertificateAuthority -Path $CaPath
        $validationCallback = {
            param(
                [System.Net.Http.HttpRequestMessage]$RequestMessage,
                [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
                [System.Security.Cryptography.X509Certificates.X509Chain]$Chain,
                [System.Net.Security.SslPolicyErrors]$SslPolicyErrors
            )

            if ($null -eq $Certificate) { return $false }
            if (($SslPolicyErrors -band [System.Net.Security.SslPolicyErrors]::RemoteCertificateNameMismatch) -ne 0) {
                return $false
            }
            if (($SslPolicyErrors -band [System.Net.Security.SslPolicyErrors]::RemoteCertificateNotAvailable) -ne 0) {
                return $false
            }

            $customChain = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
            try {
                $customChain.ChainPolicy.TrustMode = [System.Security.Cryptography.X509Certificates.X509ChainTrustMode]::CustomRootTrust
                $customChain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
                $customChain.ChainPolicy.VerificationFlags = [System.Security.Cryptography.X509Certificates.X509VerificationFlags]::NoFlag
                [void]$customChain.ChainPolicy.CustomTrustStore.Add($trustedRoot)
                if ($null -ne $Chain) {
                    foreach ($element in @($Chain.ChainElements) | Select-Object -Skip 1) {
                        [void]$customChain.ChainPolicy.ExtraStore.Add($element.Certificate)
                    }
                }
                return $customChain.Build($Certificate)
            }
            finally {
                $customChain.Dispose()
            }
        }.GetNewClosure()
        $handler.ServerCertificateCustomValidationCallback = $validationCallback
    }

    $baseText = $BaseUri.AbsoluteUri.TrimEnd('/') + '/'
    $client = [System.Net.Http.HttpClient]::new($handler, $true)
    $client.BaseAddress = [uri]$baseText
    $client.Timeout = [TimeSpan]::FromSeconds(15)

    $networkCredential = $ApiCredential.GetNetworkCredential()
    $token = [Convert]::ToBase64String(
        [Text.Encoding]::UTF8.GetBytes("$($networkCredential.UserName):$($networkCredential.Password)"))
    $client.DefaultRequestHeaders.Authorization =
        [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Basic', $token)
    $client.DefaultRequestHeaders.Accept.Add(
        [System.Net.Http.Headers.MediaTypeWithQualityHeaderValue]::new('application/json'))
    return $client
}

function ConvertTo-ApiSegment {
    param([Parameter(Mandatory = $true)][string]$Value)
    return [uri]::EscapeDataString($Value)
}

function Invoke-RabbitMqManagementRequest {
    param(
        [Parameter(Mandatory = $true)][System.Net.Http.HttpClient]$Client,
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [object]$Body,
        [switch]$AllowNotFound
    )

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::new($Method),
        $Path.TrimStart('/'))
    try {
        if ($null -ne $Body) {
            $json = $Body | ConvertTo-Json -Depth 20 -Compress
            $request.Content = [System.Net.Http.StringContent]::new(
                $json,
                [Text.Encoding]::UTF8,
                'application/json')
        }

        $response = $Client.SendAsync($request).GetAwaiter().GetResult()
        try {
            $payload = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if ($AllowNotFound -and [int]$response.StatusCode -eq 404) {
                return $null
            }
            if (-not $response.IsSuccessStatusCode) {
                $message = if ([string]::IsNullOrWhiteSpace($payload)) { '<empty response>' } else { $payload }
                throw "RabbitMQ Management request $Method $Path failed with HTTP $([int]$response.StatusCode): $message"
            }
            if ([string]::IsNullOrWhiteSpace($payload)) {
                return $null
            }
            return $payload | ConvertFrom-Json -Depth 30
        }
        finally {
            $response.Dispose()
        }
    }
    finally {
        $request.Dispose()
    }
}

function Test-PolicyPatternMatchesQueue {
    param([object]$Policy, [string]$QueueName)
    try {
        return [regex]::IsMatch($QueueName, [string]$Policy.pattern)
    }
    catch {
        return $false
    }
}

function Test-SafeRawPolicy {
    param([object]$Policy)

    if ($null -eq $Policy) { return $false }
    $definition = $Policy.definition
    if ($null -eq $definition) { return $false }
    $overflow = [string]$definition.overflow
    $ttlProperty = $definition.PSObject.Properties['message-ttl']
    $maxBytesProperty = $definition.PSObject.Properties['max-length-bytes']
    $ttl = if ($null -eq $ttlProperty) { 0L } else { [long]$ttlProperty.Value }
    $maxBytes = if ($null -eq $maxBytesProperty) { 0L } else { [long]$maxBytesProperty.Value }
    return $overflow -eq 'drop-head' -and $ttl -gt 0 -and $maxBytes -gt 0
}

function Get-BrokerInventory {
    param([Parameter(Mandatory = $true)][System.Net.Http.HttpClient]$Client)

    $encodedVhost = ConvertTo-ApiSegment $VirtualHost
    $queues = @(Invoke-RabbitMqManagementRequest -Client $Client -Method GET -Path "/api/queues/$encodedVhost")
    $bindings = @(Invoke-RabbitMqManagementRequest -Client $Client -Method GET -Path "/api/bindings/$encodedVhost")
    $policies = @(Invoke-RabbitMqManagementRequest -Client $Client -Method GET -Path "/api/policies/$encodedVhost")

    $primaryQueue = @($queues | Where-Object { $_.name -eq $PrimaryQueueName }) | Select-Object -First 1
    $rawQueue = @($queues | Where-Object { $_.name -eq $RawQueueName }) | Select-Object -First 1
    $primaryBindings = @($bindings | Where-Object {
        $_.source -eq $ExchangeName -and
        $_.destination_type -eq 'queue' -and
        $_.destination -eq $PrimaryQueueName -and
        $_.routing_key -eq $RoutingKey
    })
    $rawBindings = @($bindings | Where-Object {
        $_.source -eq $ExchangeName -and
        $_.destination_type -eq 'queue' -and
        $_.destination -eq $RawQueueName -and
        $_.routing_key -eq $RoutingKey
    })
    $matchingRawPolicies = @($policies | Where-Object { Test-PolicyPatternMatchesQueue -Policy $_ -QueueName $RawQueueName })
    $safeRawPolicies = @($matchingRawPolicies | Where-Object { Test-SafeRawPolicy -Policy $_ })
    $unsafeRejectPolicies = @($matchingRawPolicies | Where-Object {
        $null -ne $_.definition -and [string]$_.definition.overflow -eq 'reject-publish'
    })

    return [ordered]@{
        observedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        vhost = $VirtualHost
        exchangeName = $ExchangeName
        routingKey = $RoutingKey
        primaryQueue = $primaryQueue
        rawQueue = $rawQueue
        primaryBindings = $primaryBindings
        rawBindings = $rawBindings
        policies = $policies
        matchingRawPolicies = $matchingRawPolicies
        safeRawPolicies = $safeRawPolicies
        unsafeRejectPolicies = $unsafeRejectPolicies
    }
}

function Write-JsonEvidence {
    param([Parameter(Mandatory = $true)][string]$Name, [Parameter(Mandatory = $true)]$Data)
    $path = Join-Path $runDirectory $Name
    $Data | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $path -Encoding utf8
    return $path
}

function Get-QueueConsumerCount {
    param($Queue)
    if ($null -eq $Queue) { return 0 }
    $property = $Queue.PSObject.Properties['consumers']
    if ($null -eq $property) { return 0 }
    return [int]$property.Value
}

function Assert-PrimaryBinding {
    param($Inventory)
    if (@($Inventory.primaryBindings).Count -lt 1) {
        throw "Primary binding '$ExchangeName --$RoutingKey--> $PrimaryQueueName' is absent. Refusing raw migration."
    }
}

function Assert-SafeRawProtection {
    param($Inventory)
    if (@($Inventory.safeRawPolicies).Count -lt 1) {
        throw "No safe raw policy applies to '$RawQueueName'. Apply Protect before unbinding or rollback."
    }
}

function Assert-Confirmation {
    param([Parameter(Mandatory = $true)][string]$Expected)
    if ($Confirmation -ne $Expected) {
        throw "Confirmation mismatch. Repeat with -Confirmation '$Expected'."
    }
}

function Get-Plan {
    param($Inventory)
    return [ordered]@{
        action = $Action
        applyRequested = [bool]$Apply
        primaryBindingPresent = @($Inventory.primaryBindings).Count -ge 1
        rawBindingCount = @($Inventory.rawBindings).Count
        rawQueuePresent = $null -ne $Inventory.rawQueue
        rawConsumers = Get-QueueConsumerCount -Queue $Inventory.rawQueue
        safeRawPolicyCount = @($Inventory.safeRawPolicies).Count
        unsafeRejectPolicyNames = @($Inventory.unsafeRejectPolicies | ForEach-Object { $_.name })
        automaticPurge = $false
        automaticQueueDelete = $false
        cloudMutationExecutedByPackageAuthor = $false
    }
}

$credentialToUse = Get-RequiredCredential -ProvidedCredential $Credential
$client = New-RabbitMqManagementClient `
    -BaseUri $ManagementBaseUri `
    -ApiCredential $credentialToUse `
    -CaPath $CertificateAuthorityPath `
    -PermitHttp:$AllowInsecureHttp

$before = $null
$after = $null
$mutationExecuted = $false
$verdict = 'PHASE3F_INCOMPLETE'
try {
    $before = Get-BrokerInventory -Client $client
    Write-JsonEvidence -Name 'before.json' -Data $before | Out-Null
    $plan = Get-Plan -Inventory $before
    Write-JsonEvidence -Name 'plan.json' -Data $plan | Out-Null

    $encodedVhost = ConvertTo-ApiSegment $VirtualHost
    $encodedRawPolicy = ConvertTo-ApiSegment $RawProtectionPolicyName
    $encodedLegacyPolicy = ConvertTo-ApiSegment $LegacyBroadPolicyName
    $encodedExchange = ConvertTo-ApiSegment $ExchangeName
    $encodedRawQueue = ConvertTo-ApiSegment $RawQueueName

    switch ($Action) {
        'Inventory' {
            $verdict = 'PHASE3F_INVENTORY_COMPLETE'
        }
        'Plan' {
            Assert-PrimaryBinding -Inventory $before
            $verdict = 'PHASE3F_PLAN_READY'
        }
        'Protect' {
            Assert-PrimaryBinding -Inventory $before
            if ($MessageTtlMilliseconds -le 0) { throw '-MessageTtlMilliseconds must be positive for Protect.' }
            if ($MaxLengthBytes -le 0) { throw '-MaxLengthBytes must be positive for Protect.' }
            $body = [ordered]@{
                pattern = '^' + [regex]::Escape($RawQueueName) + '$'
                'apply-to' = 'queues'
                priority = $RawPolicyPriority
                definition = [ordered]@{
                    'message-ttl' = $MessageTtlMilliseconds
                    'max-length-bytes' = $MaxLengthBytes
                    overflow = 'drop-head'
                }
            }
            if ($Apply) {
                $expected = "PROTECT_RAW:${VirtualHost}:${RawQueueName}"
                Assert-Confirmation -Expected $expected
                if ($PSCmdlet.ShouldProcess($RawQueueName, 'apply bounded drop-head migration policy')) {
                    Invoke-RabbitMqManagementRequest -Client $client -Method PUT `
                        -Path "/api/policies/$encodedVhost/$encodedRawPolicy" -Body $body | Out-Null
                    $mutationExecuted = $true
                }
                $verdict = 'PHASE3F_RAW_PROTECTION_APPLIED'
            }
            else {
                $verdict = 'PHASE3F_RAW_PROTECTION_PLAN_ONLY'
            }
        }
        'RetireLegacyPolicy' {
            Assert-PrimaryBinding -Inventory $before
            Assert-SafeRawProtection -Inventory $before
            $primaryPolicy = @($before.policies | Where-Object { $_.name -eq $PrimaryPolicyName }) | Select-Object -First 1
            if ($null -eq $primaryPolicy) {
                throw "Exact primary policy '$PrimaryPolicyName' is absent. Refusing to retire '$LegacyBroadPolicyName'."
            }
            if ($Apply) {
                $expected = "RETIRE_LEGACY_POLICY:${VirtualHost}:${LegacyBroadPolicyName}"
                Assert-Confirmation -Expected $expected
                if ($PSCmdlet.ShouldProcess($LegacyBroadPolicyName, 'remove legacy broad RabbitMQ policy')) {
                    Invoke-RabbitMqManagementRequest -Client $client -Method DELETE `
                        -Path "/api/policies/$encodedVhost/$encodedLegacyPolicy" -AllowNotFound | Out-Null
                    $mutationExecuted = $true
                }
                $verdict = 'PHASE3F_LEGACY_POLICY_RETIRED'
            }
            else {
                $verdict = 'PHASE3F_LEGACY_POLICY_RETIREMENT_PLAN_ONLY'
            }
        }
        'Unbind' {
            Assert-PrimaryBinding -Inventory $before
            $rawConsumers = Get-QueueConsumerCount -Queue $before.rawQueue
            if ($rawConsumers -gt 0) {
                throw "Raw queue has $rawConsumers consumer(s). Refusing unbind until ownership is resolved."
            }
            if ($null -ne $before.rawQueue) {
                Assert-SafeRawProtection -Inventory $before
            }
            if ($Apply) {
                $expected = "UNBIND_RAW:${VirtualHost}:${ExchangeName}:${RawQueueName}:${RoutingKey}"
                Assert-Confirmation -Expected $expected
                foreach ($binding in @($before.rawBindings)) {
                    if ([string]::IsNullOrWhiteSpace([string]$binding.properties_key)) {
                        throw 'Raw binding has no properties_key; refusing deletion through an ambiguous endpoint.'
                    }
                    $encodedPropertiesKey = ConvertTo-ApiSegment ([string]$binding.properties_key)
                    if ($PSCmdlet.ShouldProcess($RawQueueName, "remove binding properties_key=$($binding.properties_key)")) {
                        Invoke-RabbitMqManagementRequest -Client $client -Method DELETE `
                            -Path "/api/bindings/$encodedVhost/e/$encodedExchange/q/$encodedRawQueue/$encodedPropertiesKey" | Out-Null
                        $mutationExecuted = $true
                    }
                }
                $verdict = 'PHASE3F_RAW_BINDING_REMOVED'
            }
            else {
                $verdict = 'PHASE3F_RAW_UNBIND_PLAN_ONLY'
            }
        }
        'Verify' {
            Assert-PrimaryBinding -Inventory $before
            if (@($before.rawBindings).Count -ne 0) {
                throw "Raw binding remains present ($(@($before.rawBindings).Count))."
            }
            if ($null -ne $before.rawQueue) {
                Assert-SafeRawProtection -Inventory $before
            }
            if (@($before.unsafeRejectPolicies).Count -gt 0) {
                $names = @($before.unsafeRejectPolicies | ForEach-Object { $_.name }) -join ', '
                throw "Unsafe reject-publish policy still matches raw queue: $names"
            }
            $verdict = 'PHASE3F_RAW_DISABLED_AND_UNBOUND'
        }
        'Rollback' {
            Assert-PrimaryBinding -Inventory $before
            if ($null -eq $before.rawQueue) {
                throw 'Raw queue is absent. This rollback does not create queues automatically.'
            }
            Assert-SafeRawProtection -Inventory $before
            if ($Apply) {
                $expected = "ROLLBACK_RAW:${VirtualHost}:${ExchangeName}:${RawQueueName}:${RoutingKey}"
                Assert-Confirmation -Expected $expected
                if (@($before.rawBindings).Count -eq 0 -and $PSCmdlet.ShouldProcess($RawQueueName, 'restore bounded raw binding')) {
                    Invoke-RabbitMqManagementRequest -Client $client -Method POST `
                        -Path "/api/bindings/$encodedVhost/e/$encodedExchange/q/$encodedRawQueue" `
                        -Body ([ordered]@{ routing_key = $RoutingKey; arguments = @{} }) | Out-Null
                    $mutationExecuted = $true
                }
                $verdict = 'PHASE3F_RAW_BINDING_ROLLBACK_APPLIED'
            }
            else {
                $verdict = 'PHASE3F_RAW_BINDING_ROLLBACK_PLAN_ONLY'
            }
        }
    }

    $after = Get-BrokerInventory -Client $client
    Write-JsonEvidence -Name 'after.json' -Data $after | Out-Null

    if ($Action -eq 'Protect' -and $Apply -and @($after.safeRawPolicies | Where-Object { $_.name -eq $RawProtectionPolicyName }).Count -ne 1) {
        throw 'Raw protection policy was not observed after apply.'
    }
    if ($Action -eq 'RetireLegacyPolicy' -and $Apply -and @($after.policies | Where-Object { $_.name -eq $LegacyBroadPolicyName }).Count -ne 0) {
        throw 'Legacy broad policy remains after retirement.'
    }
    if ($Action -eq 'Unbind' -and $Apply -and @($after.rawBindings).Count -ne 0) {
        throw 'Raw binding remains after unbind.'
    }
    if ($Action -eq 'Rollback' -and $Apply -and @($after.rawBindings).Count -lt 1) {
        throw 'Raw binding was not observed after rollback.'
    }

    $result = [ordered]@{
        schemaVersion = '1.0'
        action = $Action
        verdict = $verdict
        managementAuthority = "$($ManagementBaseUri.Scheme)://$($ManagementBaseUri.Authority)"
        vhost = $VirtualHost
        mutationRequested = [bool]$Apply
        mutationExecuted = $mutationExecuted
        automaticPurge = $false
        automaticQueueDelete = $false
        evidenceDirectory = $runDirectory
        completedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    }
    Write-JsonEvidence -Name 'result.json' -Data $result | Out-Null
    Write-Host $verdict
    Write-Host "Evidence directory: $runDirectory"
}
finally {
    if ($null -ne $client) {
        $client.Dispose()
    }
}
