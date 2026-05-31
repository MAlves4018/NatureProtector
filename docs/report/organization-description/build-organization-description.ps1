$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $scriptDir
try {
    latexmk -pdf -interaction=nonstopmode -file-line-error organization-description.tex
} finally {
    Pop-Location
}
