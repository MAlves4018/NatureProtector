# Arquitetura PostgreSQL

## Finalidade do documento

Este documento consolida, numa só leitura, o papel do `PostgreSQL` no estado atual do NatureProtector.

Os nove documentos de fase em [`../planning/`](../planning/) continuam úteis como historial incremental, mas já não são a forma mais eficiente de perceber o sistema como ele existe hoje.

Para a visão arquitetural global, ver [architecture.md](architecture.md). Para o guia operacional do que já pode ser corrido hoje, ver [current-capabilities-and-how-to-run.md](current-capabilities-and-how-to-run.md).

## Como ler este documento

1. Se queres perceber rapidamente o papel do `PostgreSQL`, lê `Papel arquitetural`, `Schemas atuais` e `Fluxos ativos`.
2. Se queres explorar o sistema na prática, vai para `O que já podemos explorar hoje`.
3. Se queres perceber como esta arquitetura nasceu, lê `Relação com as fases PostgreSQL`.

## Papel arquitetural do PostgreSQL

No estado atual do projeto, o `PostgreSQL` já não é apenas uma intenção nem apenas armazenamento de configuração.

Ele cumpre três papéis complementares:

| Papel | Schema | O que guarda |
| --- | --- | --- |
| plano de controlo | `control` | configuração ativa, área, grelha, sensores, cenários, artefactos de dataset e `simulation_runs` |
| estado durável da pipeline | `pipeline` | inbox, tentativas, rejeições técnicas e quarentena |
| estado operacional projetado | `projection` | logs duráveis, estado por célula, estado por área e alertas ativos simples |

O `PostgreSQL` liga hoje:

- a baseline de dados e os manifests no sistema de ficheiros;
- o simulador quando trabalha em modo com plano de controlo;
- a pipeline operacional do `Prevention.Host`;
- a superfície HTTP do `Backoffice.Api`.

## O que o PostgreSQL não substitui

| Camada | Fonte principal | Porque não vive toda em PostgreSQL |
| --- | --- | --- |
| artefactos preparados | `data/baseline/` e `data/manifests/` | os ficheiros continuam a ser os artefactos físicos versionados |
| transporte de eventos | `RabbitMQ` | o broker continua a ser o mecanismo de entrega |
| séries temporais operacionais | `InfluxDB` | retenção temporal e observabilidade não são tratadas como problema relacional |

## Componentes que tocam diretamente em PostgreSQL

| Componente | Papel |
| --- | --- |
| [`../../src/NatureProtector.Postgres.Bootstrap/`](../../src/NatureProtector.Postgres.Bootstrap/) | aplica migrations e materializa o plano de controlo inicial |
| [`../../src/NatureProtector.Infrastructure.Postgres/`](../../src/NatureProtector.Infrastructure.Postgres/) | define records, `DbContext`, migrations e serviços de suporte |
| [`../../src/NatureProtector.Simulator.Host/`](../../src/NatureProtector.Simulator.Host/) | lê área, cenário e sensores do `control` e regista `simulation_runs` |
| [`../../src/NatureProtector.Prevention.Host/`](../../src/NatureProtector.Prevention.Host/) | usa `pipeline` e `projection` como ponto de commit durável e consulta operacional |
| [`../../src/NatureProtector.Backoffice.Api/`](../../src/NatureProtector.Backoffice.Api/) | expõe `control` e `projection` por HTTP |

O ponto de entrada técnico comum é [`../../src/NatureProtector.Infrastructure.Postgres/Persistence/NatureProtectorControlDbContext.cs`](../../src/NatureProtector.Infrastructure.Postgres/Persistence/NatureProtectorControlDbContext.cs).

## Schemas atuais

### Schema `control`

O schema `control` guarda a configuração que o sistema resolve antes de executar.

| Tabela | Papel | Como é preenchida hoje |
| --- | --- | --- |
| `configuration_versions` | versão ativa da configuração | bootstrap |
| `areas` | área piloto e metadados base | bootstrap |
| `area_contexts` | contexto agregado de área | preparada, ainda não semeada pelo bootstrap atual |
| `grid_cells` | grelha operacional por célula | bootstrap |
| `sensor_profiles` | perfis de ruído, falha e publicação | bootstrap |
| `sensor_networks` | rede lógica de sensores | bootstrap |
| `sensor_nodes` | sensores ativos usados pelo simulador | bootstrap |
| `scenario_definitions` | cenários executáveis e parâmetros em JSON | bootstrap |
| `simulation_runs` | runs concretas do simulador | runtime do `Simulator.Host` |
| `rule_set_versions` | versões de regras | preparada, ainda sem uso ativo relevante |
| `dataset_artifacts` | catálogo dos artefactos preparados | bootstrap |
| `scenario_dataset_bindings` | ligações cenário -> artefactos | bootstrap |

O bootstrap atual, implementado em [`../../src/NatureProtector.Infrastructure.Postgres/Bootstrap/ControlPlaneBootstrapper.cs`](../../src/NatureProtector.Infrastructure.Postgres/Bootstrap/ControlPlaneBootstrapper.cs), faz estas operações:

1. aplica migrations;
2. cria ou atualiza a configuração `v1`;
3. indexa artefactos do plano de datasets;
4. importa a área `proenca-a-nova`;
5. importa as células da grelha;
6. cria perfis de sensor piloto;
7. gera a rede piloto de sensores;
8. importa os cenários `A/B/C`;
9. cria bindings entre cenários e artefactos relevantes.

### Schema `pipeline`

O schema `pipeline` é o ponto de commit durável da ingestão.

| Tabela | Papel |
| --- | --- |
| `event_inbox` | registo único do envelope recebido e do seu estado de processamento |
| `processing_attempts` | histórico das tentativas por evento |
| `rejected_events` | rejeições técnicas, incluindo payload bruto inválido |
| `quarantined_events` | falhas permanentes ou esgotamento da política de retry |

Os estados atuais da inbox, definidos em [`../../src/NatureProtector.Infrastructure.Postgres/Pipeline/PipelineRecords.cs`](../../src/NatureProtector.Infrastructure.Postgres/Pipeline/PipelineRecords.cs), são:

- `Pending`
- `Processing`
- `Processed`
- `Failed`
- `Rejected`
- `RetryPending`
- `Quarantined`

Em linguagem prática:

- `RetryPending` significa que o evento vai voltar a ser tentado mais tarde;
- um retry pronto a ser retomado é apenas um retry cujo momento mínimo de reprocessamento já chegou;
- `Quarantined` significa que o sistema desistiu de reprocessar automaticamente.

### Schema `projection`

O schema `projection` contém o rasto operacional e a vista atual do estado do subsistema.

| Tabela | Papel | Escrita principal |
| --- | --- | --- |
| `accepted_reading_log` | log durável das leituras aceites | `ReadingRiskPipeline` |
| `risk_assessment_log` | log durável dos assessments | `ReadingRiskPipeline` |
| `area_risk_snapshot_log` | snapshots agregados por área | `ReadingRiskPipeline` |
| `cell_operational_state` | estado operacional mais recente por célula | `PostgresAreaOperationalProjectionStore` |
| `area_operational_state` | estado operacional mais recente por área | `PostgresAreaOperationalProjectionStore` |
| `alert_state` | alertas ativos simples associados ao estado de área | `PostgresAreaOperationalProjectionStore` |

## Fluxos ativos ligados a PostgreSQL

### 1. Bootstrap do plano de controlo

Fluxo:

1. levantar a baseline local;
2. correr [`../../scripts/postgres/bootstrap-control-plane.ps1`](../../scripts/postgres/bootstrap-control-plane.ps1);
3. aplicar migrations;
4. carregar área, grelha, sensores, cenários e artefactos;
5. deixar `control` pronto para consumo pelos hosts.

O esquema real não depende de `init.sql` no `docker-compose`. É criado pelas migrations e preenchido pelo bootstrap.

### 2. Resolução do contexto de simulação

Quando `Simulator:ControlPlaneEnabled = true`, o fluxo é este:

1. o `Simulator.Host` resolve a área por `AreaId` ou `ControlPlaneAreaCode`;
2. resolve o cenário por `ScenarioId` ou `ControlPlaneScenarioCode`;
3. carrega sensores ativos e respetivos perfis;
4. cria a `simulation_run`;
5. publica eventos com `event_time` lógico;
6. atualiza o ciclo de vida da run em `control.simulation_runs`.

Hotspots:

- [`../../src/NatureProtector.Simulator.Host/Services/PostgresSimulationContextSource.cs`](../../src/NatureProtector.Simulator.Host/Services/PostgresSimulationContextSource.cs)
- [`../../src/NatureProtector.Simulator.Host/Services/PostgresSimulationRunStore.cs`](../../src/NatureProtector.Simulator.Host/Services/PostgresSimulationRunStore.cs)

### 3. Commit durável da pipeline de prevenção

Quando `PreventionHost:PipelinePersistenceEnabled = true`, o fluxo é este:

1. a mensagem chega a `np.ingestion.readings`;
2. o envelope é persistido em `pipeline.event_inbox`;
3. é criada a primeira tentativa em `pipeline.processing_attempts`;
4. só depois disso o `ack` é enviado ao broker;
5. a pipeline de risco escreve em `projection`;
6. retries e quarentena são geridos a partir de `pipeline`.

Hotspots:

- [`../../src/NatureProtector.Prevention.Host/PreventionWorker.cs`](../../src/NatureProtector.Prevention.Host/PreventionWorker.cs)
- [`../../src/NatureProtector.Prevention.Host/Processing/ReadingEventProcessingService.cs`](../../src/NatureProtector.Prevention.Host/Processing/ReadingEventProcessingService.cs)
- [`../../src/NatureProtector.Prevention.Host/Processing/InboxRetryWorker.cs`](../../src/NatureProtector.Prevention.Host/Processing/InboxRetryWorker.cs)
- [`../../src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs`](../../src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs)

### 4. Leitura operacional por API

Quando `BackofficeApi:ControlPlaneEnabled = true`, a API lê `control` e `projection` para expor configurações, áreas, grelha, sensores, cenários, `simulation_runs`, estado operacional e alertas.

## O que já podemos explorar hoje

### Levantar e popular

```powershell
.\infra\scripts\up.ps1
.\infra\scripts\smoke-test.ps1
.\scripts\postgres\bootstrap-control-plane.ps1
```

### Inspecionar tabelas por schema

```powershell
docker exec np-postgres psql -U np -d natureprotector -c "\dt control.*"
docker exec np-postgres psql -U np -d natureprotector -c "\dt pipeline.*"
docker exec np-postgres psql -U np -d natureprotector -c "\dt projection.*"
```

### Confirmar o plano de controlo

```powershell
docker exec np-postgres psql -U np -d natureprotector -c "select version_number, is_active from control.configuration_versions;"
docker exec np-postgres psql -U np -d natureprotector -c "select code, name from control.areas;"
docker exec np-postgres psql -U np -d natureprotector -c "select code, scenario_kind from control.scenario_definitions order by code;"
docker exec np-postgres psql -U np -d natureprotector -c "select name, type, is_active from control.sensor_nodes order by name limit 12;"
```

### Confirmar runs, retries e projeções

```powershell
docker exec np-postgres psql -U np -d natureprotector -c "select scenario_code, status, created_at from control.simulation_runs order by created_at desc limit 10;"
docker exec np-postgres psql -U np -d natureprotector -c "select status, count(*) from pipeline.event_inbox group by status order by status;"
docker exec np-postgres psql -U np -d natureprotector -c "select count(*) from pipeline.rejected_events;"
docker exec np-postgres psql -U np -d natureprotector -c "select count(*) from pipeline.quarantined_events;"
docker exec np-postgres psql -U np -d natureprotector -c "select aggregate_risk_level, severity, snapshot_timestamp from projection.area_operational_state;"
```

### Confirmar a mesma informação por API

```powershell
Invoke-RestMethod http://localhost:5254/api/control/configurations/active
Invoke-RestMethod http://localhost:5254/api/control/areas
Invoke-RestMethod http://localhost:5254/api/control/areas/proenca-a-nova/scenarios
Invoke-RestMethod http://localhost:5254/api/control/areas/proenca-a-nova/operational-state
Invoke-RestMethod 'http://localhost:5254/api/control/simulation-runs?areaCode=proenca-a-nova'
```

## Relação com as fases PostgreSQL

| Fase | O que deixou de forma durável no repositório |
| --- | --- |
| 1 | congelou a ideia de `control`, `pipeline` e `projection` |
| 2 | introduziu a base do modelo de controlo e do projeto `Infrastructure.Postgres` |
| 3 | fechou migrations, `DbContext` e bootstrap inicial |
| 4 | ligou sensores piloto, `simulation_runs` e o simulador ao `control` |
| 5 | abriu a primeira superfície HTTP do plano de controlo |
| 6 | introduziu a inbox durável e o `ack` depois do commit |
| 7 | acrescentou retries internos e quarentena persistida |
| 8 | abriu o schema `projection` e a primeira leitura operacional por API |
| 9 | tornou duráveis os logs operacionais e a projeção por célula |

Em termos práticos: as fases já foram absorvidas pelo código atual. Ler as fases uma a uma continua a ser útil para investigação histórica, mas já não é a forma mais eficiente de entender o sistema.

## Estado atual e lacunas ainda abertas

### O que já está implementado

- `PostgreSQL` como plano de controlo real;
- `simulation_runs` persistidas pelo simulador;
- inbox durável;
- retries internos e quarentena;
- logs duráveis de leituras, assessments e snapshots;
- estado operacional por área e por célula;
- API de leitura sobre `control` e `projection`.

### O que está apenas parcialmente fechado

- `area_contexts` e `rule_set_versions` existem no modelo, mas ainda não são o centro real da runtime;
- a ponte formal dataset -> cenário -> run ainda não está totalmente fechada como trilho único de auditoria;
- a semântica `accepted / rejected / normalized` ainda não existe como família completa de eventos operacionais independentes;
- o replay assistido da quarentena ainda não existe;
- os alertas continuam simples e sem ciclo de vida rico.

## Leituras seguintes

- [architecture.md](architecture.md) para o contexto arquitetural completo.
- [current-capabilities-and-how-to-run.md](current-capabilities-and-how-to-run.md) para os percursos operacionais.
- [repository-exploration-guide.md](repository-exploration-guide.md) para ligar esta arquitetura ao código.
- [`../../src/NatureProtector.Infrastructure.Postgres/README.md`](../../src/NatureProtector.Infrastructure.Postgres/README.md) para o módulo de persistência.
- [`../../src/NatureProtector.Postgres.Bootstrap/README.md`](../../src/NatureProtector.Postgres.Bootstrap/README.md) para o bootstrap.
