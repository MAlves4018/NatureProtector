<#
.SYNOPSIS
Executa `dotnet` com o ambiente local do repositório já configurado.

.DESCRIPTION
Este wrapper aplica primeiro o ambiente isolado do repositório e depois invoca
o CLI do .NET. Para comandos que costumam sofrer com paralelismo agressivo,
injeta `-m:1` quando o utilizador não especifica outra política.

.NOTES
- Aceita todos os argumentos restantes e passa-os diretamente a `dotnet`.
- É útil para builds e testes locais mais previsíveis.
#>

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$DotnetArgs
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

if ($null -eq $DotnetArgs -or $DotnetArgs.Count -eq 0) {
    throw "Usage: .\\scripts\\dotnet\\Invoke-RepoDotnet.ps1 <dotnet-args>"
}

& (Join-Path $PSScriptRoot "Use-RepoDotnetEnvironment.ps1") -Quiet

$effectiveArgs = @($DotnetArgs)
$supportsSingleProc = @("build", "test", "restore", "publish")
$hasSingleProcOverride = $effectiveArgs | Where-Object { $_ -match '^(--disable-parallel|-m(?::|$)|/m(?::|$)|-maxcpucount(?::|$)|/maxcpucount(?::|$))' }

# Força execução single-process apenas quando o comando normalmente o suporta e
# o utilizador ainda não definiu esse comportamento.
if ($effectiveArgs.Count -gt 0 -and $supportsSingleProc -contains $effectiveArgs[0].ToLowerInvariant() -and -not $hasSingleProcOverride) {
    if ($effectiveArgs.Count -eq 1) {
        $effectiveArgs = @($effectiveArgs[0], "-m:1")
    }
    else {
        $effectiveArgs = @($effectiveArgs[0], "-m:1") + $effectiveArgs[1..($effectiveArgs.Count - 1)]
    }
}

Push-Location $repoRoot
try {
    & dotnet @effectiveArgs
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
