#Para executar correr na raiz do projeto o comando comando:
#.\infra\scripts\logs.ps1

# Move para a raiz do projeto
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path (Join-Path $ScriptDir "..\..")
Set-Location $ProjectRoot

# Mostra logs em tempo real
docker compose logs -f