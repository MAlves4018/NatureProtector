#Para executar correr na raiz do projeto o comando comando:
#.\infra\scripts\down.ps1

# Move para a raiz do projeto
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path (Join-Path $ScriptDir "..\..")
Set-Location $ProjectRoot

# Desce os serviços
docker compose down