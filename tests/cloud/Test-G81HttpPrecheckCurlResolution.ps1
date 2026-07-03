Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$scriptPath = Join-Path $repoRoot "scripts/cloud/Deploy-G81Staging.ps1"

$text = Get-Content `
    -LiteralPath $scriptPath `
    -Raw

if ($text -notmatch 'function\s+Resolve-CurlExecutable') {
    throw "Resolve-CurlExecutable function is missing."
}

if ($text -notmatch 'Get-Command') {
    throw "curl resolution must use Get-Command."
}

if ($text -notmatch 'CommandType\s+Application') {
    throw "curl resolution must restrict lookup to executable applications."
}

if ($text -notmatch 'Select-Object\s+-First\s+1') {
    throw "curl resolution must select exactly one executable."
}

if ($text -match '\$curl\s*=\s*\(?\s*Get-Command\s+curl\b[^\r\n]*\.Source') {
    throw "Unsafe direct Get-Command curl .Source assignment detected."
}

if ($text -match '/usr/bin/curl /bin/curl') {
    throw "Joined curl paths regression detected."
}

if ($text -match '&\s+\$curlCommands') {
    throw "The curl command collection must not be invoked directly."
}

if ($text -match '&\s+"\$curl') {
    throw "curl must not be invoked through a quoted interpolated command string."
}

$start = $text.IndexOf("function Test-HttpPrecheck")
$end = $text.IndexOf("function Test-G81PreSmokeReadiness")

if ($start -lt 0) {
    throw "Test-HttpPrecheck function was not found."
}

if ($end -lt 0 -or $end -le $start) {
    throw "Unable to determine the end of Test-HttpPrecheck."
}

$testHttpPrecheckBody = $text.Substring($start, $end - $start)

$curlInvocationCount = [regex]::Matches(
    $testHttpPrecheckBody,
    '&\s+\$curlExecutable'
).Count

if ($curlInvocationCount -ne 1) {
    throw "Test-HttpPrecheck must invoke curl exactly once. Count=$curlInvocationCount"
}

if ($testHttpPrecheckBody -notmatch '\$exit\s*=\s*\$LASTEXITCODE') {
    throw "Test-HttpPrecheck must capture LASTEXITCODE immediately after curl."
}

if ($testHttpPrecheckBody -notmatch '--connect-timeout\s+"15"') {
    throw "Test-HttpPrecheck must set an explicit connect timeout."
}

if ($testHttpPrecheckBody -notmatch '--max-time\s+"60"') {
    throw "Test-HttpPrecheck must set an explicit max time."
}

if ($testHttpPrecheckBody -notmatch '--write-out\s+"%\{http_code\}"') {
    throw "Test-HttpPrecheck must write the HTTP status code."
}

Write-Output "G81_HTTP_PRECHECK_CURL_RESOLUTION_TEST=PASS"
