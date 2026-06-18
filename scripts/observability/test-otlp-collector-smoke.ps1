param(
    [Parameter(Mandatory = $true)]
    [string]$ApiPublishDirectory,
    [string]$CollectorImage = "otel/opentelemetry-collector-contrib:0.130.0",
    [string]$OutputRoot = "artifacts/observability/otlp-collector-smoke",
    [int]$StartupTimeoutSeconds = 45
)

$ErrorActionPreference = "Stop"

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Wait-ForFileContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Pattern,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds([Math]::Max(1, $TimeoutSeconds))
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            $content = Get-Content -LiteralPath $Path -Raw
            if ($content -match $Pattern) {
                return $content
            }
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Timed out waiting for pattern '$Pattern' in $Path"
}

function Start-PackageApi {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ApiDirectory,
        [Parameter(Mandatory = $true)]
        [string]$OtlpEndpoint,
        [Parameter(Mandatory = $true)]
        [string]$EvidenceDirectory,
        [int]$TimeoutSeconds
    )

    $apiDll = Join-Path $ApiDirectory "NatureProtector.Backoffice.Api.dll"
    if (-not (Test-Path -LiteralPath $apiDll)) {
        throw "Backoffice API DLL not found: $apiDll"
    }

    $port = Get-FreeTcpPort
    $baseUrl = "http://127.0.0.1:$port"
    $stdoutPath = Join-Path $EvidenceDirectory "backoffice-api.stdout.log"
    $stderrPath = Join-Path $EvidenceDirectory "backoffice-api.stderr.log"

    $previousEnvironment = @{
        "ASPNETCORE_URLS" = $env:ASPNETCORE_URLS
        "ASPNETCORE_ENVIRONMENT" = $env:ASPNETCORE_ENVIRONMENT
        "DOTNET_ENVIRONMENT" = $env:DOTNET_ENVIRONMENT
        "BackofficeApi__ControlPlaneEnabled" = $env:BackofficeApi__ControlPlaneEnabled
        "Observability__ConsoleExporterEnabled" = $env:Observability__ConsoleExporterEnabled
        "OTEL_EXPORTER_OTLP_ENDPOINT" = $env:OTEL_EXPORTER_OTLP_ENDPOINT
        "OTEL_METRIC_EXPORT_INTERVAL" = $env:OTEL_METRIC_EXPORT_INTERVAL
    }

    try {
        $env:ASPNETCORE_URLS = $baseUrl
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $env:DOTNET_ENVIRONMENT = "Development"
        $env:BackofficeApi__ControlPlaneEnabled = "false"
        $env:Observability__ConsoleExporterEnabled = "false"
        $env:OTEL_EXPORTER_OTLP_ENDPOINT = $OtlpEndpoint
        $env:OTEL_METRIC_EXPORT_INTERVAL = "1000"

        $process = Start-Process `
            -FilePath "dotnet" `
            -ArgumentList @($apiDll) `
            -WorkingDirectory $ApiDirectory `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath `
            -WindowStyle Hidden `
            -PassThru

        $deadline = (Get-Date).AddSeconds([Math]::Max(1, $TimeoutSeconds))
        $lastError = $null
        while ((Get-Date) -lt $deadline) {
            if ($process.HasExited) {
                throw "Backoffice API exited before /health was ready. ExitCode=$($process.ExitCode)"
            }

            try {
                $response = Invoke-WebRequest -Uri "$baseUrl/health" -UseBasicParsing -TimeoutSec 2
                if ([int]$response.StatusCode -eq 200) {
                    return [ordered]@{
                        process = $process
                        baseUrl = $baseUrl
                        healthStatusCode = [int]$response.StatusCode
                    }
                }
            }
            catch {
                $lastError = $_.Exception.Message
            }

            Start-Sleep -Milliseconds 250
        }

        throw "Backoffice API /health did not become ready within $TimeoutSeconds seconds. Last error: $lastError"
    }
    finally {
        foreach ($key in $previousEnvironment.Keys) {
            if ($null -eq $previousEnvironment[$key]) {
                Remove-Item "env:$key" -ErrorAction SilentlyContinue
            }
            else {
                Set-Item "env:$key" $previousEnvironment[$key]
            }
        }
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$apiDirectory = (Resolve-Path $ApiPublishDirectory).Path
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $repoRoot (Join-Path $OutputRoot $timestamp)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$grpcPort = Get-FreeTcpPort
$containerName = "np-otel-smoke-$($timestamp.Replace('-', ''))"
$tracesPath = Join-Path $runDirectory "traces.json"
$metricsPath = Join-Path $runDirectory "metrics.json"
$collectorConfigPath = Join-Path $runDirectory "otelcol-config.yaml"

@"
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
exporters:
  file/traces:
    path: /evidence/traces.json
  file/metrics:
    path: /evidence/metrics.json
service:
  pipelines:
    traces:
      receivers: [otlp]
      exporters: [file/traces]
    metrics:
      receivers: [otlp]
      exporters: [file/metrics]
"@ | Set-Content -LiteralPath $collectorConfigPath -Encoding UTF8

$api = $null
$collectorStarted = $false

try {
    & docker run `
        -d `
        --rm `
        --name $containerName `
        -p "127.0.0.1:$grpcPort`:4317" `
        -v "$runDirectory`:/evidence" `
        $CollectorImage `
        "--config=/evidence/otelcol-config.yaml" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start OpenTelemetry Collector container."
    }

    $collectorStarted = $true
    Start-Sleep -Seconds 2

    $api = Start-PackageApi `
        -ApiDirectory $apiDirectory `
        -OtlpEndpoint "http://127.0.0.1:$grpcPort" `
        -EvidenceDirectory $runDirectory `
        -TimeoutSeconds $StartupTimeoutSeconds

    for ($i = 0; $i -lt 3; $i++) {
        Invoke-WebRequest -Uri "$($api.baseUrl)/health" -UseBasicParsing -TimeoutSec 2 | Out-Null
        Start-Sleep -Milliseconds 250
    }

    Start-Sleep -Seconds 4

    if ($api.process -and -not $api.process.HasExited) {
        Stop-Process -Id $api.process.Id -Force -ErrorAction SilentlyContinue
        $api.process.WaitForExit(5000) | Out-Null
    }

    & docker stop $containerName | Out-Null
    $collectorStarted = $false

    $traceContent = Wait-ForFileContent -Path $tracesPath -Pattern "NatureProtector.Backoffice.Api" -TimeoutSeconds 5
    $metricContent = Wait-ForFileContent -Path $metricsPath -Pattern "NatureProtector.Backoffice.Api" -TimeoutSeconds 5

    $manifest = [ordered]@{
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        collectorImage = $CollectorImage
        collectorContainer = $containerName
        otlpEndpoint = "http://127.0.0.1:$grpcPort"
        apiPublishDirectory = $apiDirectory
        apiHealth = [ordered]@{
            status = "passed"
            url = "$($api.baseUrl)/health"
            statusCode = $api.healthStatusCode
        }
        traces = [ordered]@{
            status = "received"
            path = $tracesPath
            containsServiceName = $traceContent -match "NatureProtector.Backoffice.Api"
            containsHealthRoute = $traceContent -match "/health|GET /health"
        }
        metrics = [ordered]@{
            status = "received"
            path = $metricsPath
            containsServiceName = $metricContent -match "NatureProtector.Backoffice.Api"
            containsHttpOrRuntimeMetric = $metricContent -match "http|process|runtime|aspnetcore"
        }
        status = "ready"
        scope = "Local OpenTelemetry Collector smoke with OTLP gRPC receiver and file exporters. Proves that Backoffice.Api can deliver at least one trace payload and one metrics payload to a real collector. It does not prove remote collector operation, dashboard queries or full cross-service correlation."
    }

    $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $runDirectory "otlp-collector-smoke-result.json") -Encoding UTF8
    Write-Host "OTLP collector smoke complete: $runDirectory"
}
finally {
    if ($api -and $api.process -and -not $api.process.HasExited) {
        Stop-Process -Id $api.process.Id -Force -ErrorAction SilentlyContinue
        $api.process.WaitForExit(5000) | Out-Null
    }

    if ($collectorStarted) {
        & docker stop $containerName | Out-Null
    }
}
