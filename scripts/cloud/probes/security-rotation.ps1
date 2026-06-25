#!/usr/bin/env pwsh
& (Join-Path $PSScriptRoot 'Invoke-G82ProbeAdapter.ps1') -Action 'security-rotation'
exit $LASTEXITCODE
