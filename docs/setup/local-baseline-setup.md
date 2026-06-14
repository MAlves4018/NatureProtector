# ConfiguraÃ§Ã£o da baseline local

Este documento descreve o caminho **clone-to-run** para correr a baseline local do NatureProtector em ambiente `Development`.

O objetivo Ã© permitir que uma pessoa clone o repositÃ³rio, crie a configuraÃ§Ã£o local, suba a infraestrutura, inicialize a base de dados, instale a webUI, faÃ§a login e execute uma run no Run Orchestrator.

> Fonte principal: este documento deve ser seguido antes de usar instruÃ§Ãµes antigas dispersas por outros ficheiros. O `Simulator.Host` nÃ£o deve ser arrancado manualmente no fluxo normal; deve ser lanÃ§ado pelo Run Orchestrator.

---

## 1. Fluxo suportado

```text
Run Orchestrator
  -> Simulator.Host
  -> RabbitMQ
  -> Prevention.Host
  -> PostgreSQL / InfluxDB
  -> Backoffice.Api / Grafana
  -> webUI
```

Responsabilidades principais:

- `infra/scripts/up.ps1` sobe a infraestrutura Docker: PostgreSQL, RabbitMQ, InfluxDB e Grafana.
- `scripts/postgres/bootstrap-control-plane.ps1` inicializa/importa a baseline da base de dados local.
- `scripts/dev/start-local-runtime.ps1` arranca `Backoffice.Api`, `Prevention.Host` e `webUI` em background.
- `Simulator.Host` Ã© lanÃ§ado pelo Run Orchestrator quando uma run Ã© pedida.
- Depois da run terminar, `Simulator.Host` deve fechar.

---

## 2. PrÃ©-requisitos

Instalar antes de iniciar:

- PowerShell.
- Git.
- Docker Desktop com Docker Engine ativo.
- Docker Compose v2.
- .NET SDK usado pela soluÃ§Ã£o.
- Node.js e npm.

ValidaÃ§Ã£o read-only:

```powershell
.\scripts\setup\Test-LocalPrerequisites.ps1
```

`dotnet-ef` **nÃ£o Ã© obrigatÃ³rio** para o fluxo normal clone-to-run. A baseline local Ã© inicializada pelo script `scripts/postgres/bootstrap-control-plane.ps1`. `dotnet-ef` fica reservado para validaÃ§Ã£o avanÃ§ada/desenvolvimento.

---

## 3. Preparar `.env` 

Depois de clonar o repositÃ³rio:

```powershell
Copy-Item .\.env.example .\.env
```

O `.env.example` jÃ¡ inclui um `INFLUXDB_TOKEN` local/dev de conveniÃªncia para a baseline acadÃ©mica. NÃ£o Ã© preciso gerar um token manualmente no caminho normal clone-to-run. O ficheiro `.env` Ã© local, contÃ©m credenciais de desenvolvimento e nÃ£o deve ser versionado.

---

## 4. Subir infraestrutura Docker

Executar:

```powershell
.\infra\scripts\up.ps1
```

O script:

- resolve a raiz do repositÃ³rio;
- valida o ficheiro `docker-compose.yml`;
- cria `.env` a partir de `.env.example` se faltar;
- cria/atualiza o ficheiro local de token admin do InfluxDB;
- executa Docker Compose;
- garante a database InfluxDB `np_telemetry`;
- nÃ£o arranca API, Prevention Host nem webUI.

Verificar containers:

```powershell
docker ps -a --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

Depois de subir os containers, inicializar a base de dados no passo seguinte antes de executar a validaÃ§Ã£o completa da baseline.

### Nota sobre `dotnet test` e infraestrutura local

Antes de executar a suite de testes completa, garantir que a infraestrutura Docker local estÃ¡ ativa:

```powershell
.\infra\scripts\up.ps1
```

Alguns testes, nomeadamente testes de API/integraÃ§Ã£o, dependem dos serviÃ§os locais expostos pela baseline, como PostgreSQL, RabbitMQ e restantes dependÃªncias configuradas no ambiente local. Por isso, num clone novo ou apÃ³s limpeza de containers, deve-se subir a infraestrutura antes de correr:

```powershell
dotnet test .\NatureProtector.sln --no-restore --nologo -v minimal -m:1
```

Fluxo recomendado para validaÃ§Ã£o tÃ©cnica:

```powershell
.\infra\scripts\up.ps1

dotnet build .\NatureProtector.sln --nologo -v minimal --configfile NuGet.Config

dotnet test .\NatureProtector.sln --no-restore --nologo -v minimal -m:1
```

Se o runtime local jÃ¡ estiver ativo, ver primeiro a secÃ§Ã£o de troubleshooting sobre ficheiros bloqueados durante o build.

---

## 5. Inicializar a base de dados local

Num clone novo ou depois de limpar volumes Docker, executar:

```powershell
.\scripts\postgres\bootstrap-control-plane.ps1
```

Este script prepara a baseline local no PostgreSQL e importa:

- Ã¡rea `proenca-a-nova`;
- grelha;
- sensores;
- cenÃ¡rios;
- bindings de cenÃ¡rios;
- configuraÃ§Ã£o base necessÃ¡ria ao control plane.

Validar novamente:

```powershell
.\scripts\setup\Test-LocalBaseline.ps1 -InfrastructureOnly
```

Resultado esperado depois da inicializaÃ§Ã£o completa:

```text
[OK] PostgreSQL container - np-postgres is running
[OK] RabbitMQ container - np-rabbitmq is running
[OK] InfluxDB container - np-influxdb is running
[OK] Grafana container - np-grafana is running
[OK] PostgreSQL control schema - 12 table(s) found
[OK] InfluxDB database - np_telemetry exists
[OK] Grafana - http://localhost:3000/api/health returned HTTP 200

Summary: 0 required failure(s), 0 total failure(s), 0 warning(s).
```

VerificaÃ§Ã£o SQL opcional:

```powershell
@'
select table_schema, table_name
from information_schema.tables
where table_schema in ('control', 'pipeline', 'projection')
order by table_schema, table_name;
'@ | docker exec -i np-postgres psql -U np -d natureprotector
```

Resultado esperado no estado atual: tabelas nos schemas `control`, `pipeline` e `projection`.

---

## 6. Instalar dependÃªncias da webUI

Num clone novo, `webUI/node_modules` ainda nÃ£o existe. Instalar as dependÃªncias frontend antes de arrancar o runtime local.

Como o projeto tem `package-lock.json`, usar:

```powershell
cd .\webUI
npm ci
cd ..
```

Se `package-lock.json` nÃ£o existir num checkout futuro, usar `npm install`.

Se este passo for ignorado, o launcher pode falhar com erro semelhante a:

```text
'vite' is not recognized as an internal or external command
```

ou:

```text
webUI did not become reachable on TCP port 5173 within 60 seconds
```

---

## 7. Arrancar runtime local

Executar:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev\start-local-runtime.ps1 -OpenBrowser -ForceRestart
```

Ou se jÃ¡ tiver o Browser aberto:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev\start-local-runtime.ps1 -ForceRestart
```

O launcher:

- usa `docker compose --project-directory <repo> -f <repo>\docker-compose.yml up -d`;
- arranca `Backoffice.Api`, `Prevention.Host` e `webUI` em background;
- espera API e webUI ficarem acessÃ­veis antes de abrir o browser;
- nÃ£o segue logs em foreground;
- devolve o prompt;
- escreve logs em `docs/evidence/dev-runtime/<timestamp>/`.

Mensagem final esperada:

```text
Launcher completed. Services continue in background.
Logs: <runRoot>
```

Se falhar, consultar os logs indicados no erro:

```text
docs/evidence/dev-runtime/<timestamp>/
```

Ficheiros principais:

- `backoffice-api.log`;
- `backoffice-api.err.log`;
- `prevention-host.log`;
- `prevention-host.err.log`;
- `webui.log`;
- `webui.err.log`.

---

## 8. Login local em Development

Abrir:

```text
http://localhost:5173
```

Credenciais locais de Development:

```text
Username: admin
Password: admin123
```

Estas credenciais sÃ£o apenas para baseline local/Development. NÃ£o usar fora de desenvolvimento.

Depois do login:

```text
Scenario Lab -> Run Orchestrator
```

### Identidades locais Pipeline/Sim

Para reproduzir jornadas por perfil sem criar roles novas, usar o script de preparacao local:

```powershell
$env:NP_DEMO_ADMIN_PASSWORD = "<admin-local-password>"
$env:NP_DEMO_PIPELINE_PASSWORD = "<pipeline-local-password>"
$env:NP_DEMO_SIM_PASSWORD = "<sim-local-password>"

.\scripts\setup\Ensure-LocalDemoIdentities.ps1 -ValidateJourneys
```

O script usa apenas `api/users-roles/login` e os endpoints Admin existentes de `api/users-roles/users`. As roles `Admin`, `Sim` e `Pipeline` ja existem na baseline; o script nao cria roles, nao muda claims, nao altera schema e nao grava passwords no repositorio.

Validacoes feitas pelo script quando `-ValidateJourneys` e usado:

- `Pipeline`: login, leitura de runtime permitida, tentativa de arrancar run negada com `403`.
- `Sim`: login, selecao de cenarios permitida, arranque de run minima permitido e `run id` devolvido.

O arranque `Sim` e um side effect local: adiciona uma run curta ao PostgreSQL. Usar apenas quando esse estado for aceitavel ou depois de decidir um rebaseline explicito.

---

## 9. Correr `scenario_b` no Run Orchestrator

Usar parÃ¢metros de smoke local:

```text
Scenario: scenario_b
Degradation profile: none
Sensors: 6
Cycles: 5
Interval seconds: 5
Seed: 12345
```

Com 6 sensores Ã— 5 ciclos, o esperado Ã©:

```text
30 eventos processados
30 processing attempts bem sucedidas
30 risk assessments
sem db_data_exception
sem quarentena inesperada
Simulator.Host fechado apÃ³s a run
```

---

## 10. Validar `scenario_b`

Runs recentes:

```powershell
@'
select "Id", "ScenarioCode", "StartedAt", "EndedAt", "Status"
from control.simulation_runs
order by "StartedAt" desc
limit 5;
'@ | docker exec -i np-postgres psql -U np -d natureprotector
```

Tentativas de processamento recentes:

```powershell
@'
select "Outcome", "ErrorCode", count(*) as count
from pipeline.processing_attempts
where "StartedAt" > now() - interval '30 minutes'
group by "Outcome", "ErrorCode"
order by count desc;
'@ | docker exec -i np-postgres psql -U np -d natureprotector
```

Risk assessments recentes:

```powershell
@'
select count(*) as risk_assessments,
       min("RiskScore") as min_score,
       max("RiskScore") as max_score
from projection.risk_assessment_log
where "CreatedAt" > now() - interval '30 minutes';
'@ | docker exec -i np-postgres psql -U np -d natureprotector
```

Confirmar que `Simulator.Host` fechou:

```powershell
Get-CimInstance Win32_Process |
  Where-Object { $_.CommandLine -like "*NatureProtector.Simulator.Host*" } |
  Select-Object ProcessId, ParentProcessId, CreationDate, CommandLine
```

Exemplo de evidÃªncia obtida em validaÃ§Ã£o local:

```text
ScenarioCode: scenario_b
RunId: 90251536-3009-44e3-9e11-778609c7a370
StartedAt: 2026-06-01 14:46:55.80722+00
EndedAt: 2026-06-01 14:47:16.081746+00
Status: 3
processing_attempts: 30
ErrorCode: vazio
risk_assessments: 30
min_score: 0.41778972000000003
max_score: 0.4482019516
Simulator.Host: sem processo apÃ³s a run
```

---

## 11. Correr e validar `scenario_c`

Depois de validar `scenario_b`, correr `scenario_c` pelo Run Orchestrator.

Usar, no mÃ­nimo:

```text
Scenario: scenario_c
Degradation profile: missing-readings
Sensors: 6
Cycles: 5
Interval seconds: 5
Seed: 12345
```

Validar:

- a run termina;
- o perfil de degradacao fica registado nos overrides/metadata;
- existem eventos em falta quando esperado;
- continuam a existir risk assessments;
- nao ha quarentena inesperada;
- `Simulator.Host` fecha apos a run.

Usar as mesmas queries do `scenario_b` e, quando aplicavel, a vista de comparacao/evidencia da UI.

---

## 12. Validacao completa da baseline

A validacao completa pode ser executada com:

```powershell
.\scripts\setup\Test-LocalBaseline.ps1 -Full
```

No estado atual, este comando usa `/health` para readiness publico da Backoffice API e valida `api/control/configurations/active` como guarda de autenticacao esperada:

```text
[OK] Backoffice API health - http://localhost:5254/health returned HTTP 200
[OK] Backoffice API auth guard - http://localhost:5254/api/control/configurations/active returned HTTP 401; authenticated endpoint is protected as expected
```

Um `401` neste endpoint nao e falha de readiness: e requisito de autenticacao. O endpoint continua protegido por `Sim`, `Pipeline` ou `Admin`.

Para validacao operacional do setup, usar:

- `Test-LocalBaseline.ps1 -InfrastructureOnly`;
- login na UI com `admin` / `admin123`;
- run real no Run Orchestrator;
- queries SQL de validacao.

---

## 13. ValidaÃ§Ã£o avanÃ§ada de migrations

Este passo Ã© opcional e destinado a desenvolvimento/auditoria. NÃ£o Ã© necessÃ¡rio para executar a baseline local se o bootstrap tiver concluÃ­do com sucesso.

Instalar `dotnet-ef`, se necessÃ¡rio:

```powershell
dotnet tool install --global dotnet-ef --version 9.*
```

Depois:

```powershell
dotnet ef migrations has-pending-model-changes `
  --project .\src\NatureProtector.Infrastructure.Postgres\NatureProtector.Infrastructure.Postgres.csproj `
  --startup-project .\src\NatureProtector.Postgres.Bootstrap\NatureProtector.Postgres.Bootstrap.csproj `
  --context NatureProtectorControlDbContext
```

E, se for necessÃ¡rio aplicar migrations manualmente:

```powershell
dotnet ef database update `
  --project .\src\NatureProtector.Infrastructure.Postgres\NatureProtector.Infrastructure.Postgres.csproj `
  --startup-project .\src\NatureProtector.Postgres.Bootstrap\NatureProtector.Postgres.Bootstrap.csproj `
  --context NatureProtectorControlDbContext
```

---

## 14. Troubleshooting

### PowerShell bloqueia scripts

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup\Test-LocalPrerequisites.ps1
```

### Troubleshooting: `dotnet build` falha com DLLs bloqueadas

Se `dotnet build` falhar com mensagens semelhantes a:

```text
The file is locked by: "NatureProtector.Backoffice.Api"
The file is locked by: "NatureProtector.Prevention.Host"
```

significa que o runtime local ainda estÃ¡ ativo em background. Isto pode acontecer depois de correr:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev\start-local-runtime.ps1 -OpenBrowser -ForceRestart
```

O launcher arranca a Backoffice API, o Prevention Host e a webUI em processos separados e depois devolve o prompt. Esse comportamento Ã© esperado para a demo, mas os processos .NET continuam a usar DLLs em `bin\Debug\net9.0`. Enquanto estiverem vivos, o Windows pode impedir que o `dotnet build` substitua esses ficheiros.

### Confirmar processos ativos

```powershell
Get-CimInstance Win32_Process |
  Where-Object {
    $_.CommandLine -like "*NatureProtector.Backoffice.Api*" -or
    $_.CommandLine -like "*NatureProtector.Prevention.Host*" -or
    $_.CommandLine -like "*NatureProtector.Simulator.Host*" -or
    $_.CommandLine -like "*NatureProtector\webUI*" -or
    $_.CommandLine -like "*vite*"
  } |
  Select-Object ProcessId, ParentProcessId, CreationDate, CommandLine
```

### Parar os processos locais do projeto

```powershell
Get-CimInstance Win32_Process |
  Where-Object {
    $_.CommandLine -like "*NatureProtector.Backoffice.Api*" -or
    $_.CommandLine -like "*NatureProtector.Prevention.Host*" -or
    $_.CommandLine -like "*NatureProtector.Simulator.Host*" -or
    $_.CommandLine -like "*NatureProtector\webUI*" -or
    $_.CommandLine -like "*vite*"
  } |
  ForEach-Object {
    Stop-Process -Id $_.ProcessId -Force
  }
```

### Confirmar que pararam

```powershell
Get-CimInstance Win32_Process |
  Where-Object {
    $_.CommandLine -like "*NatureProtector.Backoffice.Api*" -or
    $_.CommandLine -like "*NatureProtector.Prevention.Host*" -or
    $_.CommandLine -like "*NatureProtector.Simulator.Host*" -or
    $_.CommandLine -like "*NatureProtector\webUI*" -or
    $_.CommandLine -like "*vite*"
  } |
  Select-Object ProcessId, CommandLine
```

O resultado esperado Ã© nÃ£o aparecer nenhum processo.

Depois repetir:

```powershell
dotnet build .\NatureProtector.sln --nologo -v minimal --configfile NuGet.Config
```

### Regra prÃ¡tica

* Para validar build/testes: runtime local parado.
* Para validar demo/run no orquestrador: runtime local ativo.
* Se o build falhar por ficheiros bloqueados, parar os processos locais e repetir o build.


### Docker indisponÃ­vel

- abrir Docker Desktop;
- esperar o engine ficar pronto;
- repetir:

```powershell
.\infra\scripts\up.ps1
```

### `INFLUXDB_TOKEN` invÃ¡lido

O `.env.example` atual jÃ¡ inclui um token local/dev para a baseline acadÃ©mica. Se o token no `.env` tiver sido removido, truncado ou substituÃ­do por um valor invÃ¡lido, recriar `.env` a partir de `.env.example` Ã© o caminho preferido:

```powershell
Copy-Item .\.env.example .\.env -Force
.\infra\scripts\up.ps1
```

Gerar um token manualmente sÃ³ deve ser necessÃ¡rio para investigaÃ§Ã£o avanÃ§ada:

```powershell
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$token = "apiv3_" + ([Convert]::ToBase64String($bytes).Replace("+","-").Replace("/","_").TrimEnd("="))
(Get-Content .env) -replace '^INFLUXDB_TOKEN=.*$', "INFLUXDB_TOKEN=$token" | Set-Content .env -Encoding UTF8
```

Depois repetir:

```powershell
.\infra\scripts\up.ps1
```

### `vite` nÃ£o Ã© reconhecido

Causa provÃ¡vel: dependÃªncias frontend nÃ£o instaladas.

```powershell
cd .\webUI
npm ci
cd ..
```

Depois:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev\start-local-runtime.ps1 -OpenBrowser -ForceRestart
```

### API/webUI nÃ£o ficam ready no launcher

Ver logs indicados no erro:

```text
docs/evidence/dev-runtime/<timestamp>/
```

### Porta ocupada

Usar `-ForceRestart` apenas para processos locais do NatureProtector:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev\start-local-runtime.ps1 -OpenBrowser -ForceRestart
```

Se a porta pertencer a outro processo, parar manualmente o processo ou alterar a porta em `.env`.

### Control plane vazio

Em base de dados nova ou depois de `docker compose down -v`:

```powershell
.\scripts\postgres\bootstrap-control-plane.ps1
.\scripts\setup\Test-LocalBaseline.ps1 -InfrastructureOnly
```

### `Test-LocalBaseline.ps1 -Full` reporta auth guard em `401`

O `401` esperado de `api/control/configurations/active` deve aparecer como `[OK] Backoffice API auth guard`. Se aparecer como `[FAIL]`, o script local esta desatualizado ou a alteracao nao foi aplicada. Confirmar a baseline por:

```powershell
.\scripts\setup\Test-LocalBaseline.ps1 -Full
```

e depois validar login/run pela UI ou por `Ensure-LocalDemoIdentities.ps1 -ValidateJourneys`.

### Reset/rebaseline seguro antes de uma demo limpa

Nao apagar volumes para limpar apenas runs. O caminho seguro e:

1. Inspecionar runs e contagens atuais.
2. Executar dry-run do reset runtime pela API.
3. Se uma demo limpa for mesmo necessaria, executar reset runtime confirmado.
4. Criar uma nova run curta com label explicita.
5. Validar por summary/audit/timings e registar o `run id` escolhido.

Dry-run via API, usando uma identidade `Sim` ou `Admin`:

```powershell
$login = Invoke-RestMethod `
  -Method POST `
  -Uri "http://localhost:5254/api/users-roles/login" `
  -ContentType "application/json" `
  -Body (@{ usernameOrEmail = "sim.local"; password = $env:NP_DEMO_SIM_PASSWORD } | ConvertTo-Json)

$headers = @{ Authorization = "Bearer $($login.token)" }

Invoke-RestMethod `
  -Method POST `
  -Uri "http://localhost:5254/api/control/runtime/reset" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body (@{ scope = "runtime-only"; confirm = ""; dryRun = $true } | ConvertTo-Json)
```

Reset real, apenas com decisao explicita:

```powershell
Invoke-RestMethod `
  -Method POST `
  -Uri "http://localhost:5254/api/control/runtime/reset" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body (@{ scope = "runtime-only"; confirm = "RESET_RUNTIME_STATE"; dryRun = $false } | ConvertTo-Json)
```

Este reset e Development-only, exige a confirmacao textual exata, bloqueia se houver run ativa e nao apaga Docker volumes. Ele limpa estado runtime/pipeline/projection suportado pelo endpoint; tabelas de control plane como areas, sensores, cenarios e configuracoes ficam preservadas. Nao usar como evidencia cientifica nem como limpeza de secrets.

### Reset destrutivo

NÃ£o usar no fluxo normal. Apenas com confirmaÃ§Ã£o explÃ­cita:

```powershell
.\infra\scripts\reset-local-infra.ps1 -Confirm RESET_LOCAL_INFRA
```

Este comando apaga volumes locais da baseline.

---

## 15. Repor o ambiente para simular um clone novo

Este capÃ­tulo Ã© para testar o setup desde um estado equivalente a alguÃ©m que acabou de clonar o repositÃ³rio.

> AtenÃ§Ã£o: os comandos abaixo apagam containers, volumes e ficheiros locais ignorados. NÃ£o usar se houver evidÃªncia local que ainda precise de ser guardada.

### 15.1 Parar processos locais do NatureProtector

Listar processos:

```powershell
Get-CimInstance Win32_Process |
  Where-Object {
    $_.CommandLine -like "*NatureProtector.Backoffice.Api*" -or
    $_.CommandLine -like "*NatureProtector.Prevention.Host*" -or
    $_.CommandLine -like "*NatureProtector.Simulator.Host*" -or
    $_.CommandLine -like "*NatureProtector\webUI*" -or
    $_.CommandLine -like "*vite*"
  } |
  Select-Object ProcessId, ParentProcessId, CreationDate, CommandLine
```

Parar processos:

```powershell
Get-CimInstance Win32_Process |
  Where-Object {
    $_.CommandLine -like "*NatureProtector.Backoffice.Api*" -or
    $_.CommandLine -like "*NatureProtector.Prevention.Host*" -or
    $_.CommandLine -like "*NatureProtector.Simulator.Host*" -or
    $_.CommandLine -like "*NatureProtector\webUI*" -or
    $_.CommandLine -like "*vite*"
  } |
  ForEach-Object {
    Stop-Process -Id $_.ProcessId -Force
  }
```

### 15.2 Remover containers e volumes Docker

```powershell
docker compose --project-directory . -f .\docker-compose.yml down -v --remove-orphans
```

Confirmar:

```powershell
docker ps -a
```

### 15.3 Apagar estado local ignorado

Ver primeiro o que seria apagado:

```powershell
git clean -ndX
```

Se a lista fizer sentido:

```powershell
git clean -fdX
```

Isto remove ficheiros ignorados, como:

- `.env`;
- `bin/`;
- `obj/`;
- `node_modules/`;
- `data/runtime/`;
- logs locais;
- caches locais.

NÃ£o usar `git clean -fd` sem `-X`, porque isso pode apagar ficheiros untracked nÃ£o ignorados.

### 15.4 Restaurar evidÃªncia versionada removida por engano

Se tiverem sido apagados ficheiros tracked de `docs/evidence/runs`, restaurar:

```powershell
git restore docs/evidence/runs
```

Confirmar estado:

```powershell
git status --short --branch
```

### 15.5 Validar que o estado estÃ¡ prÃ³ximo de clone novo

```powershell
Test-Path .env
Test-Path data\runtime
Test-Path .\webUI\node_modules
docker ps -a
git ls-files .env .env.example
```

Esperado:

```text
.env -> False
data/runtime -> False
webUI/node_modules -> False
docker ps -a -> sem containers da baseline
git ls-files -> .env nÃ£o aparece; .env.example aparece
```

Depois regressar ao passo 3 deste documento e seguir o setup completo.
