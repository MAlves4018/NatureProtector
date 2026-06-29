[CmdletBinding()]
param(
    [string]$Path = "scripts",
    [string]$Settings = "PSScriptAnalyzerSettings.psd1"
)

$ErrorActionPreference = "Stop"
if (-not (Get-Module -ListAvailable -Name PSScriptAnalyzer)) {
    throw "PSScriptAnalyzer is not installed. Install the version declared in config/quality/quality-gates.json."
}

$findings = @(Invoke-ScriptAnalyzer -Path $Path -Recurse -Settings $Settings)
$findings | Sort-Object ScriptPath, Line, Column, RuleName | Format-Table -AutoSize
if ($findings.Count -gt 0) {
    Write-Error "PSScriptAnalyzer reported $($findings.Count) finding(s)."
    exit 1
}
exit 0
