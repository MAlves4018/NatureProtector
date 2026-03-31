#Para executar correr na raiz do projeto o comando comando:
#.\infra\scripts\smoke-test.ps1

# Move para a raiz do projeto
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path (Join-Path $ScriptDir "..\..")
Set-Location $ProjectRoot

# Mostra o estado dos containers
docker compose ps