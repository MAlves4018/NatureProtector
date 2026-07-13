# Capacidades Atuais e Como Executar

## Finalidade do documento

Este documento reúne apenas o que já é realmente possível fazer agora com o estado atual do repositório.

Serve para responder, de forma operacional, a estas perguntas:

- o que já consigo correr;
- que pré-condições tenho;
- que comando devo usar;
- o que devo esperar em cada passo;
- onde posso verificar que o sistema está vivo.

Para clone-to-run, a fonte principal é [docs/setup/local-baseline-setup.md](../setup/local-baseline-setup.md), com `scripts/np.ps1` como entrypoint recomendado. Este documento é uma referência técnica complementar para perceber componentes, verificações e caminhos manuais/diagnósticos.

Para a explicação arquitetural detalhada, ver [architecture.md](architecture.md). Para o detalhe consolidado do papel do `PostgreSQL`, ver [postgresql-architecture.md](postgresql-architecture.md). Para um percurso de leitura do código e dos docs, ver [repository-exploration-guide.md](repository-exploration-guide.md).

## O que este documento cobre e o que não cobre

Este documento cobre:

- baseline local com Docker Compose;
- bootstrap do plano de controlo em `PostgreSQL`;
- arranque de `Backoffice.Api`, `Prevention.Host` e webUI;
- execução normal de simulações pelo Run Orchestrator, que lança `Simulator.Host` por run;
- o que já é observável em `RabbitMQ`, `PostgreSQL`, `InfluxDB`, `Grafana` e na API;
- percursos práticos para validar o fluxo end-to-end.

Este documento não cobre:

- wishlist funcional;
- refatorações futuras;
- arquitetura-alvo ainda não implementada;
- a pipeline completa de construção de dados da área piloto, que está documentada em [`../../scripts/data/README.md`](../../scripts/data/README.md).

## Panorama rápido do que já podemos fazer hoje

| Capacidade | Estado | Ponto de entrada principal |
| --- | --- | --- |
| levantar baseline local com broker, base relacional, Influx e Grafana | `Implementado` | [`../../scripts/np.ps1`](../../scripts/np.ps1) `up` |
| materializar o plano de controlo em `PostgreSQL` | `Implementado` | [`../../scripts/postgres/bootstrap-control-plane.ps1`](../../scripts/postgres/bootstrap-control-plane.ps1) |
| arrancar a API de consulta do plano de controlo e do estado operacional | `Implementado` | [`../../src/NatureProtector.Backoffice.Api/Program.cs`](../../src/NatureProtector.Backoffice.Api/Program.cs) |
| executar uma simulação pelo fluxo normal | `Implementado` | Run Orchestrator na webUI/API; `Simulator.Host` é lançado por run |
| consumir eventos e materializar estado operacional durável | `Implementado` | [`../../src/NatureProtector.Prevention.Host/Program.cs`](../../src/NatureProtector.Prevention.Host/Program.cs) |
| observar o broker e a topologia principal | `Implementado` | `RabbitMQ Management` |
| observar schemas `control`, `pipeline` e `projection` | `Implementado` | `PostgreSQL` |
| observar telemetria em `InfluxDB` | `Implementado`, quando `InfluxDb:Enabled=true` e existe token válido | `Prevention.Host` + `InfluxDB` |
| usar Grafana como observabilidade de apoio | `Parcial` | baseline local |

## Pré-condições mínimas

Antes de arrancar o sistema, assume estas condições.

- Windows e PowerShell.
- Docker Desktop funcional.
- `.NET 9 SDK`.
- A baseline de dados da área piloto já existe no repositório em [`../../data/baseline/areas/proenca-a-nova/`](../../data/baseline/areas/proenca-a-nova/).
- Os manifests de cenário já existem em [`../../data/manifests/scenarios/`](../../data/manifests/scenarios/).

Nota importante para o `Prevention.Host`:

- o ficheiro [`../../src/NatureProtector.Prevention.Host/appsettings.json`](../../src/NatureProtector.Prevention.Host/appsettings.json) vem com `InfluxDb:Token` vazio;
- por omissão, `InfluxDb:Enabled=true`, pelo que caso `InfluxDb:Enabled=false` o host usa `NoOpInfluxWriteService` e consegue processar a pipeline operacional sem token de `InfluxDB`;
- quando `InfluxDb:Enabled=true`, o host aplica fallback por variáveis de ambiente e por `.env` através de [`../../src/NatureProtector.Infrastructure.Influx/Configuration/InfluxDbSettingsLoader.cs`](../../src/NatureProtector.Infrastructure.Influx/Configuration/InfluxDbSettingsLoader.cs);
- nesse modo ativo, se o token efetivo não existir, o writer real de `InfluxDB` falha cedo na configuração.

Pré-condição importante para os comandos `dotnet`:

- usa sempre [`../../scripts/dotnet/Use-RepoDotnetEnvironment.ps1`](../../scripts/dotnet/Use-RepoDotnetEnvironment.ps1) antes de `build`, `test` ou `run`.

## Baseline local: como levantar

### Comandos

```powershell
.\scripts\np.ps1 doctor
.\scripts\np.ps1 init-local -Force
.\scripts\np.ps1 prepare-local
.\scripts\np.ps1 up
.\scripts\np.ps1 start
.\scripts\np.ps1 health
```

### O que estes scripts fazem

- [`../../scripts/np.ps1`](../../scripts/np.ps1) é o entrypoint recomendado para clone-to-run local; `prepare-local` restaura .NET e instala a webUI a partir dos lockfiles;
- [`../../scripts/workspace.ps1`](../../scripts/workspace.ps1) permanece como compatibilidade para fluxos antigos;
- [`../../infra/scripts/up.ps1`](../../infra/scripts/up.ps1) e o wrapper de baixo nivel de Docker Compose; exige `.env` existente e nao cria nem altera esse ficheiro;
- [`../../scripts/influx/Ensure-InfluxDatabase.ps1`](../../scripts/influx/Ensure-InfluxDatabase.ps1) e idempotente e pode ser corrido manualmente para confirmar/criar `np_telemetry`;
- [`../../infra/scripts/smoke-test.ps1`](../../infra/scripts/smoke-test.ps1) mostra o estado dos contentores.

### Serviços e portas esperados

| Serviço | Porta esperada | Origem |
| --- | --- | --- |
| `RabbitMQ` | `5672` | [`../../.env.example`](../../.env.example) |
| `RabbitMQ Management` | `15672` | [`../../.env.example`](../../.env.example) |
| `PostgreSQL` | `5433` | [`../../.env.example`](../../.env.example) |
| `InfluxDB` | `8181` | [`../../.env.example`](../../.env.example) |
| `Grafana` | `3000` | [`../../.env.example`](../../.env.example) |

### O que esperar

- contentores `np-rabbitmq`, `np-postgres`, `np-influxdb` e `np-grafana` visíveis em `docker compose ps`;
- o `RabbitMQ` expõe a interface de gestão;
- o `PostgreSQL` fica acessível em `localhost:5433` pela configuração local por omissão.

Credenciais práticas da baseline por omissão:

- `RabbitMQ Management`: `http://localhost:15672` com `np / np_dev_pass`;
- `Grafana`: `http://localhost:3000` com `admin / admin`.

## Bootstrap do plano de controlo: como correr e o que esperar

### Comando recomendado

```powershell
.\scripts\postgres\bootstrap-control-plane.ps1
```

### O que o script faz

- resolve `POSTGRES_HOST`/`POSTGRES_PORT` por ambiente ou `.env` e valida esse endpoint exato;
- prepara o ambiente `dotnet` local ao repositório;
- compila a solução, salvo se usares `-SkipBuild`;
- corre [`../../src/NatureProtector.Postgres.Bootstrap/Program.cs`](../../src/NatureProtector.Postgres.Bootstrap/Program.cs).

### O que este bootstrap materializa

Segundo o código atual e a documentação do projeto [`../../src/NatureProtector.Postgres.Bootstrap/README.md`](../../src/NatureProtector.Postgres.Bootstrap/README.md), o processo carrega:

- `control.configuration_versions`
- `control.areas`
- `control.grid_cells`
- `control.sensor_profiles`
- `control.sensor_networks`
- `control.sensor_nodes`
- `control.scenario_definitions`
- `control.dataset_artifacts`
- `control.scenario_dataset_bindings`

### Resultado esperado

O programa escreve um resumo com pelo menos:

- versão de configuração ativa;
- área importada;
- número de células da grelha;
- número de perfis de sensor;
- número de sensores;
- número de cenários;
- número de artefactos de dataset indexados.

Nota útil:

- o bootstrap não semeia `control.simulation_runs`; essa tabela fica preparada e passa a ser escrita pelo `Simulator.Host`.

## Como arrancar os hosts atuais

### 1. API

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Backoffice.Api
```

O perfil de desenvolvimento define `http://localhost:5254` em [`../../src/NatureProtector.Backoffice.Api/Properties/launchSettings.json`](../../src/NatureProtector.Backoffice.Api/Properties/launchSettings.json).

### 2. Prevention.Host

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Prevention.Host
```

Condição prática:

- não é necessário token de `InfluxDB` para a baseline operacional caso `InfluxDb:Enabled=false`;
- Por default o estado é `InfluxDb:Enabled=true`, logo confirma primeiro que existe token efetivo por `appsettings`, variável de ambiente ou `.env`.

Por omissão, [`../../src/NatureProtector.Prevention.Host/appsettings.json`](../../src/NatureProtector.Prevention.Host/appsettings.json) usa:

- `PipelinePersistenceEnabled = true`
- `InfluxDb:Enabled = true`
- `MaxProcessingAttempts = 3`
- `RetryDelaySeconds = [5, 30]`
- `RetryPollingIntervalSeconds = 5`

### 3. Simulator.Host

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Simulator.Host
```

Por omissão, [`../../src/NatureProtector.Simulator.Host/appsettings.json`](../../src/NatureProtector.Simulator.Host/appsettings.json) usa:

- `ControlPlaneEnabled = true`
- `ControlPlaneAreaCode = "proenca-a-nova"`
- `ControlPlaneScenarioCode = "scenario_b"`
- `Seed = 12345`
- `NumberOfCycles = 20`
- `IntervalSeconds = 5`

## O que conseguimos observar em cada componente

### RabbitMQ

O que já existe hoje:

- exchange `np.events`;
- fila principal `np.ingestion.readings`;
- fila adicional `np.observability.raw`;
- routing key `simulation.reading.produced`.

Onde isto está fixado:

- [`../../src/NatureProtector.Shared/Messaging/NatureProtectorRabbitMqTopology.cs`](../../src/NatureProtector.Shared/Messaging/NatureProtectorRabbitMqTopology.cs)

Como observar:

- abrir `http://localhost:15672`;
- autenticar com `np / np_dev_pass`;
- verificar o exchange `np.events`;
- verificar que a fila `np.ingestion.readings` recebe mensagens quando o simulador está ativo.

### PostgreSQL

O que já existe hoje, quando a escrita temporal está ativa:

- schema `control` para configuração e runs;
- schema `pipeline` para inbox, tentativas, rejeições e quarentena;
- schema `projection` para logs operacionais e estado projetado.

Onde isto está definido:

- [`../../src/NatureProtector.Infrastructure.Postgres/Migrations/`](../../src/NatureProtector.Infrastructure.Postgres/Migrations/)
- [`../../src/NatureProtector.Infrastructure.Postgres/Persistence/NatureProtectorControlDbContext.cs`](../../src/NatureProtector.Infrastructure.Postgres/Persistence/NatureProtectorControlDbContext.cs)

Como observar:

```powershell
docker exec np-postgres psql -U np -d natureprotector -c "\dt control.*"
docker exec np-postgres psql -U np -d natureprotector -c "\dt pipeline.*"
docker exec np-postgres psql -U np -d natureprotector -c "\dt projection.*"
```

Consultas simples úteis depois do bootstrap:

```powershell
docker exec np-postgres psql -U np -d natureprotector -c "select count(*) from control.areas;"
docker exec np-postgres psql -U np -d natureprotector -c "select count(*) from control.sensor_nodes;"
docker exec np-postgres psql -U np -d natureprotector -c "select count(*) from control.scenario_definitions;"
```

Consultas úteis depois de correr simulador e prevenção:

```powershell
docker exec np-postgres psql -U np -d natureprotector -c "select count(*) from pipeline.event_inbox;"
docker exec np-postgres psql -U np -d natureprotector -c "select count(*) from pipeline.processing_attempts;"
docker exec np-postgres psql -U np -d natureprotector -c "select status, count(*) from pipeline.event_inbox group by status order by status;"
docker exec np-postgres psql -U np -d natureprotector -c "select count(*) from pipeline.rejected_events;"
docker exec np-postgres psql -U np -d natureprotector -c "select count(*) from pipeline.quarantined_events;"
docker exec np-postgres psql -U np -d natureprotector -c "select count(*) from projection.accepted_reading_log;"
docker exec np-postgres psql -U np -d natureprotector -c "select count(*) from projection.risk_assessment_log;"
docker exec np-postgres psql -U np -d natureprotector -c "select count(*) from projection.area_risk_snapshot_log;"
docker exec np-postgres psql -U np -d natureprotector -c "select scenario_code, status, created_at from control.simulation_runs order by created_at desc limit 10;"
docker exec np-postgres psql -U np -d natureprotector -c "select aggregate_risk_level, severity, snapshot_timestamp from projection.area_operational_state;"
```

### InfluxDB

O que já existe hoje:

- escrita de `accepted_readings`;
- escrita de `risk_assessments`;
- escrita de `area_risk_snapshots`.

Onde isto está definido:

- [`../../src/NatureProtector.Infrastructure.Influx/Services/InfluxWriteService.cs`](../../src/NatureProtector.Infrastructure.Influx/Services/InfluxWriteService.cs)

Nota prática:

- o repositório já fecha a escrita, mas não traz ainda uma frente própria de leitura ou exploração confortável;
- neste estado do projeto, a observação mais prática do efeito de escrita é feita por `Grafana` ou pela confirmação de que o `Prevention.Host` está a processar sem falhas de configuração.

### Grafana

O que já podemos fazer:

- abrir `http://localhost:3000`;
- autenticar com `admin / admin`;
- confirmar que o serviço está de pé;
- usar a baseline de dashboards como apoio de observabilidade.

Limite atual:

- a camada de dashboards ainda está numa fase mais próxima de setup do que de produto operacional final.

Leitura complementar recomendada:

- [`grafana-influx-dashboard-guide.md`](grafana-influx-dashboard-guide.md)
  - guia passo a passo para datasource, descoberta de tabelas, construção de queries e desenho dos primeiros dashboards.

### API

O que já podemos observar por HTTP:

- configurações;
- áreas;
- grelha;
- sensores;
- cenários;
- `simulation_runs`;
- estado operacional por área;
- estado operacional por célula;
- alertas ativos simples.

Nota prática:

- os endpoints de estado operacional podem devolver `404` antes de existir a primeira projeção persistida para a área.

Pontos de entrada úteis:

- [`../../src/NatureProtector.Backoffice.Api/NatureProtector.Backoffice.Api.http`](../../src/NatureProtector.Backoffice.Api/NatureProtector.Backoffice.Api.http)
- [`../../src/NatureProtector.Backoffice.Api/Controllers/ControlConfigurationsController.cs`](../../src/NatureProtector.Backoffice.Api/Controllers/ControlConfigurationsController.cs)
- [`../../src/NatureProtector.Backoffice.Api/Controllers/ControlAreasController.cs`](../../src/NatureProtector.Backoffice.Api/Controllers/ControlAreasController.cs)
- [`../../src/NatureProtector.Backoffice.Api/Controllers/ControlSimulationRunsController.cs`](../../src/NatureProtector.Backoffice.Api/Controllers/ControlSimulationRunsController.cs)

Chamadas úteis:

```powershell
Invoke-RestMethod http://localhost:5254/api/control/configurations/active
Invoke-RestMethod http://localhost:5254/api/control/areas
Invoke-RestMethod http://localhost:5254/api/control/areas/proenca-a-nova/operational-state
Invoke-RestMethod http://localhost:5254/api/control/areas/proenca-a-nova/cells/operational-state
Invoke-RestMethod http://localhost:5254/api/control/areas/proenca-a-nova/alerts/active
Invoke-RestMethod http://localhost:5254/api/control/areas/proenca-a-nova/scenarios
Invoke-RestMethod 'http://localhost:5254/api/control/simulation-runs?areaCode=proenca-a-nova'
```

## Percursos práticos de execução

### Percurso A. Levantar a baseline local

1. Correr `.\infra\scripts\up.ps1`.
2. Correr `.\infra\scripts\smoke-test.ps1`.
3. Confirmar portas `15672`, `5432`, `8181` e `3000`.

### Percurso B. Materializar o plano de controlo

1. Garantir que `PostgreSQL` está de pé.
2. Correr `.\scripts\postgres\bootstrap-control-plane.ps1`.
3. Confirmar, por API ou por `psql`, que existem áreas, sensores e cenários.

### Percurso C. Arrancar a API

1. Correr `.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1`.
2. Correr `dotnet run --project .\src\NatureProtector.Backoffice.Api`.
3. Confirmar `GET /api/control/configurations/active`.

### Percurso D. Arrancar o Prevention.Host

1. Correr `.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1`.
2. Correr `dotnet run --project .\src\NatureProtector.Prevention.Host`.
3. Esperar o host em escuta, sem falhas de configuração.
4. Se quiseres observar `InfluxDB`, ativar `InfluxDb:Enabled=true` e confirmar token válido antes de arrancar o host.

### Percurso E. Arrancar o Simulator.Host

1. Correr `.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1`.
2. Correr `dotnet run --project .\src\NatureProtector.Simulator.Host`.
3. Confirmar nos logs que foi resolvido contexto para `proenca-a-nova`, cenário `scenario_b`, seed e número de ciclos.

### Percurso F. Confirmar que o fluxo end-to-end está vivo

1. Verificar no `RabbitMQ Management` que a fila principal recebeu mensagens.
2. Verificar em `PostgreSQL` que `pipeline.event_inbox` e `pipeline.processing_attempts` têm registos.
3. Verificar em `PostgreSQL` que `projection.accepted_reading_log`, `projection.risk_assessment_log` e `projection.area_risk_snapshot_log` cresceram.
4. Confirmar por API que existem `simulation_runs` e que o estado operacional da área já é consultável.

### Percurso G. Correr em modo autónomo local

Este não é o percurso principal da fase atual, mas continua a ser suportado.

1. Arrancar a baseline local para manter `RabbitMQ` e `InfluxDB`.
2. Correr o `Prevention.Host` com `PreventionHost__PipelinePersistenceEnabled=false`.
3. Correr o `Simulator.Host` com `Simulator__ControlPlaneEnabled=false`.
4. Usar a lista de sensores e os parâmetros locais do [`../../src/NatureProtector.Simulator.Host/appsettings.json`](../../src/NatureProtector.Simulator.Host/appsettings.json).

Exemplo:

```powershell
$env:PreventionHost__PipelinePersistenceEnabled = "false"
dotnet run --project .\src\NatureProtector.Prevention.Host
```

```powershell
$env:Simulator__ControlPlaneEnabled = "false"
dotnet run --project .\src\NatureProtector.Simulator.Host
```

Leitura correta deste modo:

- continua a ser útil para diagnóstico e demonstrações simples;
- deixa de validar a resolução do plano de controlo em `PostgreSQL`;
- deixa também de materializar inbox e projeções duráveis na base relacional.

## Exemplos de “se queres ver X, faz Y”

| Se queres ver... | Faz isto | Onde confirmar |
| --- | --- | --- |
| que a infraestrutura base está de pé | `.\infra\scripts\smoke-test.ps1` | saída de `docker compose ps` |
| que o plano de controlo foi carregado | `.\scripts\postgres\bootstrap-control-plane.ps1` | resumo do bootstrap, `control.areas`, `control.sensor_nodes`, `control.scenario_definitions` |
| que a API já lê o plano de controlo | arrancar `Backoffice.Api` e chamar `GET /api/control/areas` | resposta JSON |
| que o simulador está a produzir eventos | arrancar `Simulator.Host` | logs do host e fila `np.ingestion.readings` |
| que a prevenção está a consumir | arrancar `Prevention.Host` antes do simulador | logs do host e crescimento de `pipeline.event_inbox` |
| que o risco já foi materializado | correr simulador com prevenção ativa | crescimento de `projection.risk_assessment_log` e `projection.area_risk_snapshot_log` |
| que a run ficou persistida | arrancar simulador em modo com plano de controlo | `GET /api/control/simulation-runs` |
| se existem retries ou quarentena | correr a pipeline e depois consultar `pipeline.event_inbox`, `pipeline.processing_attempts`, `pipeline.quarantined_events` | `PostgreSQL` |
| que a escrita para Influx está ligada | correr prevenção com `InfluxDb:Enabled=true` e token válido | ausência de erro de configuração e observação por `Grafana` |

## Limites atuais e notas práticas

- O `Prevention.Host` não depende de `InfluxDB` para completar a pipeline operacional por omissão; nessa configuração, `PostgreSQL` permanece o estado durável e `InfluxDB` fica desligado.
- Se `InfluxDb:Enabled=true`, é necessário token válido por `appsettings`, variável de ambiente ou `.env`.
- `Grafana` já sobe, mas a frente de dashboards ainda é mais útil como prova de baseline do que como consola operacional final.
- O modo por omissão do simulador usa `ControlPlaneEnabled = true`; se o bootstrap do `PostgreSQL` não tiver sido feito, esse caminho falha.
- Existe um modo autónomo local para o simulador, mas não é o caminho principal recomendado nesta fase.
- O estado semântico completo `accepted / rejected / normalized` ainda não está exposto como famílias autónomas de eventos; o que existe hoje é inbox durável, rejeição técnica, retries, quarentena e materialização operacional.
- Leituras emitidas pelo simulador com `OperationalState = Invalid` são rejeitadas pelo `PreventionWorker` antes da inbox e não entram na pipeline de scoring.
- Se o host for interrompido a meio do processamento de um evento já aceite pela inbox, esse registo pode ficar em `Processing` até intervenção manual, porque ainda não existe recuperação automática dessas tentativas interrompidas.
- Para o detalhe consolidado de `control`, `pipeline` e `projection`, ver [postgresql-architecture.md](postgresql-architecture.md).

## Leitura adicional para aprofundar

- [`architecture.md`](architecture.md) para o racional arquitetural completo.
- [`repository-exploration-guide.md`](repository-exploration-guide.md) para um percurso de leitura do repositório.
- [`../../README.md`](../../README.md) para o quickstart e estado geral.
- [`../../src/README.md`](../../src/README.md) para o mapa dos projetos `.NET`.
- [`../../src/NatureProtector.Simulator.Host/README.md`](../../src/NatureProtector.Simulator.Host/README.md) para o detalhe do simulador.
- [`../../src/NatureProtector.Prevention.Host/README.md`](../../src/NatureProtector.Prevention.Host/README.md) para o detalhe da pipeline.
- [`../../src/NatureProtector.Backoffice.Api/README.md`](../../src/NatureProtector.Backoffice.Api/README.md) para a API já suportada.
- [`../../scripts/data/README.md`](../../scripts/data/README.md) para a pipeline completa de dados e cenários.
