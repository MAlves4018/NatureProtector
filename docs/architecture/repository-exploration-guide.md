# Guia de Exploração do Repositório

## Finalidade do guia

Este guia existe para ajudar alguém que abre o repositório `NatureProtector` pela primeira vez e não sabe por onde começar.

O objetivo não é listar pastas sem contexto. O objetivo é mostrar:

- que ficheiros ler primeiro;
- o que procurar em cada leitura;
- como relacionar documentação, código, dados e infraestrutura;
- que partes já são centrais e relativamente estáveis;
- que partes são runtime, infraestrutura, dados ou planeamento;
- como seguir um cenário real de ponta a ponta sem se perder.

## Como usar este guia

Este guia foi pensado para leitura progressiva.

- Se tens `15` minutos, segue apenas a secção [Ordem recomendada de leitura](#ordem-recomendada-de-leitura).
- Se queres perceber como o sistema funciona hoje, lê também [Percurso recomendado para seguir um cenário end-to-end](#percurso-recomendado-para-seguir-um-cenário-end-to-end).
- Se vais mexer no código, usa [Ficheiros e hotspots que merecem leitura obrigatória](#ficheiros-e-hotspots-que-merecem-leitura-obrigatória) como mapa de entrada.

Para a leitura arquitetural completa, o documento central continua a ser [architecture.md](architecture.md).

## Visão global do repositório

O repositório organiza-se em seis zonas que convém distinguir desde o início.

| Zona | Papel | Ler primeiro |
| --- | --- | --- |
| [`../../README.md`](../../README.md) | visão global, arranque rápido e estado real do projeto | sempre |
| [`../README.md`](../README.md) | índice da documentação transversal | logo a seguir |
| [`../../src/`](../../src/README.md) | projetos `.NET`, hosts, infraestrutura e domínio | depois da arquitetura |
| [`../../data/`](../../data/README.md) | baseline de dados, manifests e artefactos da área piloto | quando já se percebe o fluxo |
| [`../../scripts/`](../../scripts/data/README.md) | automação da pipeline de dados e scripts de suporte | quando queres repetir ou alterar processos |
| [`../../infra/`](../../infra/README.md) | baseline local com Docker Compose e scripts operacionais | quando queres correr o sistema |
| [`../../tests/`](../../tests/README.md) | cobertura e pontos fortes/fracos dos testes | quando queres validar maturidade e regressões |

## Ordem recomendada de leitura

Esta é a ordem mais segura para um leitor novo.

1. [`../../README.md`](../../README.md)  
   Procura: o que existe hoje, como levantar a baseline local e quais são os três hosts principais.
2. [`../README.md`](../README.md)  
   Procura: como a documentação está distribuída e que documentos são descrição do presente ou do futuro.
3. [`architecture.md`](architecture.md)  
   Procura: cadeia completa do sistema, do dado externo até ao risco, alerta e evidência.
4. [`../../src/README.md`](../../src/README.md)  
   Procura: mapa dos projetos `.NET` e dependências entre eles.
5. [`../../src/NatureProtector.Core/README.md`](../../src/NatureProtector.Core/README.md)  
   Procura: vocabulário central do domínio.
6. [`../../src/NatureProtector.Shared/README.md`](../../src/NatureProtector.Shared/README.md)  
   Procura: envelope de eventos, payload atual e topologia RabbitMQ.
7. [`../../src/NatureProtector.Simulator.Host/README.md`](../../src/NatureProtector.Simulator.Host/README.md)  
   Procura: de onde sai o contexto da simulação e como nascem os eventos.
8. [`../../src/NatureProtector.Prevention/README.md`](../../src/NatureProtector.Prevention/README.md) e [`../../src/NatureProtector.Prevention.Host/README.md`](../../src/NatureProtector.Prevention.Host/README.md)  
   Procura: onde está o cálculo de risco e onde está a pipeline operacional real.
9. [`../../src/NatureProtector.Infrastructure.Postgres/README.md`](../../src/NatureProtector.Infrastructure.Postgres/README.md) e [`../../src/NatureProtector.Infrastructure.Influx/README.md`](../../src/NatureProtector.Infrastructure.Influx/README.md)  
   Procura: o que é persistido em `PostgreSQL` e o que vai para `InfluxDB`.
10. [`../../src/NatureProtector.Backoffice.Api/README.md`](../../src/NatureProtector.Backoffice.Api/README.md)  
    Procura: o que já está consultável por HTTP.
11. [`../../data/README.md`](../../data/README.md) e [`../../scripts/data/README.md`](../../scripts/data/README.md)  
    Procura: como a baseline da área piloto foi construída e onde vivem os cenários.
12. [`../../infra/README.md`](../../infra/README.md) e [`../../tests/README.md`](../../tests/README.md)  
    Procura: como correr o ambiente local e que partes já têm melhor cobertura de testes.

## Exploração progressiva por camadas

### Documentação global

Começa por:

- [`../../README.md`](../../README.md)
- [`../README.md`](../README.md)
- [`architecture.md`](architecture.md)
- [`../planning/project-completion-roadmap.md`](../planning/project-completion-roadmap.md)
- [`../planning/pipeline-gap-and-dependency-map.md`](../planning/pipeline-gap-and-dependency-map.md)

O que tirar daqui:

- `README.md` descreve o sistema que já corre hoje.
- `architecture.md` explica a cadeia técnica completa.
- os documentos em `docs/planning/` ajudam a separar estado atual de evolução futura.

Regra prática:

- trata `README.md` e `architecture.md` como descrição do presente;
- trata `docs/planning/` como contexto de evolução e dívida arquitetural.

### Domínio e contratos

Lê:

- [`../../src/NatureProtector.Core/README.md`](../../src/NatureProtector.Core/README.md)
- [`../../src/NatureProtector.Core/Scenarios/Scenario.cs`](../../src/NatureProtector.Core/Scenarios/Scenario.cs)
- [`../../src/NatureProtector.Core/Scenarios/ScenarioParameters.cs`](../../src/NatureProtector.Core/Scenarios/ScenarioParameters.cs)
- [`../../src/NatureProtector.Core/Scenarios/SimulationRun.cs`](../../src/NatureProtector.Core/Scenarios/SimulationRun.cs)
- [`../../src/NatureProtector.Core/Sensors/Sensor.cs`](../../src/NatureProtector.Core/Sensors/Sensor.cs)
- [`../../src/NatureProtector.Core/Sensors/SensorProfile.cs`](../../src/NatureProtector.Core/Sensors/SensorProfile.cs)
- [`../../src/NatureProtector.Core/Risk/RiskAssessment.cs`](../../src/NatureProtector.Core/Risk/RiskAssessment.cs)
- [`../../src/NatureProtector.Core/Risk/AreaRiskSnapshot.cs`](../../src/NatureProtector.Core/Risk/AreaRiskSnapshot.cs)
- [`../../src/NatureProtector.Shared/README.md`](../../src/NatureProtector.Shared/README.md)
- [`../../src/NatureProtector.Shared/Messaging/EventEnvelope.cs`](../../src/NatureProtector.Shared/Messaging/EventEnvelope.cs)
- [`../../src/NatureProtector.Shared/Contracts/Readings/SensorReadingProducedPayload.cs`](../../src/NatureProtector.Shared/Contracts/Readings/SensorReadingProducedPayload.cs)
- [`../../src/NatureProtector.Shared/Messaging/NatureProtectorRabbitMqTopology.cs`](../../src/NatureProtector.Shared/Messaging/NatureProtectorRabbitMqTopology.cs)

O que procurar:

- que conceitos pertencem ao domínio e não à infraestrutura;
- que campos viajam no envelope comum dos eventos;
- que topologia RabbitMQ o sistema assume hoje.

### Simulador

Lê:

- [`../../src/NatureProtector.Simulator.Host/README.md`](../../src/NatureProtector.Simulator.Host/README.md)
- [`../../src/NatureProtector.Simulator.Host/Program.cs`](../../src/NatureProtector.Simulator.Host/Program.cs)
- [`../../src/NatureProtector.Simulator.Host/Configuration/SimulatorOptions.cs`](../../src/NatureProtector.Simulator.Host/Configuration/SimulatorOptions.cs)
- [`../../src/NatureProtector.Simulator.Host/Configuration/GeneratedScenarioManifestLoader.cs`](../../src/NatureProtector.Simulator.Host/Configuration/GeneratedScenarioManifestLoader.cs)
- [`../../src/NatureProtector.Simulator.Host/Context/ScenarioContextFactory.cs`](../../src/NatureProtector.Simulator.Host/Context/ScenarioContextFactory.cs)
- [`../../src/NatureProtector.Simulator.Host/Services/PostgresSimulationContextSource.cs`](../../src/NatureProtector.Simulator.Host/Services/PostgresSimulationContextSource.cs)
- [`../../src/NatureProtector.Simulator.Host/Services/SeedProvider.cs`](../../src/NatureProtector.Simulator.Host/Services/SeedProvider.cs)
- [`../../src/NatureProtector.Simulator.Host/Services/SimulationRunner.cs`](../../src/NatureProtector.Simulator.Host/Services/SimulationRunner.cs)
- [`../../src/NatureProtector.Simulator.Host/Services/ReadingGenerationService.cs`](../../src/NatureProtector.Simulator.Host/Services/ReadingGenerationService.cs)
- [`../../src/NatureProtector.Simulator.Host/Publishing/RabbitMqReadingPublisher.cs`](../../src/NatureProtector.Simulator.Host/Publishing/RabbitMqReadingPublisher.cs)

O que procurar:

- de onde vem o contexto da simulação;
- como a seed fixa o comportamento pseudoaleatório;
- como o tempo lógico avança por ciclos;
- onde o simulador decide publicar em RabbitMQ em vez de escrever localmente.

### Pipeline de prevenção

Lê:

- [`../../src/NatureProtector.Prevention/README.md`](../../src/NatureProtector.Prevention/README.md)
- [`../../src/NatureProtector.Prevention.Host/README.md`](../../src/NatureProtector.Prevention.Host/README.md)
- [`../../src/NatureProtector.Prevention.Host/Program.cs`](../../src/NatureProtector.Prevention.Host/Program.cs)
- [`../../src/NatureProtector.Prevention.Host/PreventionWorker.cs`](../../src/NatureProtector.Prevention.Host/PreventionWorker.cs)
- [`../../src/NatureProtector.Prevention.Host/Processing/PostgresReadingEventInbox.cs`](../../src/NatureProtector.Prevention.Host/Processing/PostgresReadingEventInbox.cs)
- [`../../src/NatureProtector.Prevention.Host/Processing/ReadingEventProcessingService.cs`](../../src/NatureProtector.Prevention.Host/Processing/ReadingEventProcessingService.cs)
- [`../../src/NatureProtector.Prevention.Host/Processing/InboxRetryWorker.cs`](../../src/NatureProtector.Prevention.Host/Processing/InboxRetryWorker.cs)
- [`../../src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs`](../../src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs)

O que procurar:

- onde acontece o `ack` ao broker;
- onde a inbox passa a ser durável;
- onde se decide retry, rejeição ou quarentena;
- onde a leitura aceite se transforma em assessment, snapshot e estado operacional.

### Persistência

Lê:

- [`../../src/NatureProtector.Infrastructure.Postgres/README.md`](../../src/NatureProtector.Infrastructure.Postgres/README.md)
- [`../../src/NatureProtector.Infrastructure.Postgres/Persistence/NatureProtectorControlDbContext.cs`](../../src/NatureProtector.Infrastructure.Postgres/Persistence/NatureProtectorControlDbContext.cs)
- [`../../src/NatureProtector.Infrastructure.Postgres/Bootstrap/ControlPlaneBootstrapper.cs`](../../src/NatureProtector.Infrastructure.Postgres/Bootstrap/ControlPlaneBootstrapper.cs)
- [`../../src/NatureProtector.Infrastructure.Postgres/Control/ControlRecords.cs`](../../src/NatureProtector.Infrastructure.Postgres/Control/ControlRecords.cs)
- [`../../src/NatureProtector.Infrastructure.Postgres/Pipeline/PipelineRecords.cs`](../../src/NatureProtector.Infrastructure.Postgres/Pipeline/PipelineRecords.cs)
- [`../../src/NatureProtector.Infrastructure.Postgres/Projection/ProjectionRecords.cs`](../../src/NatureProtector.Infrastructure.Postgres/Projection/ProjectionRecords.cs)
- [`../../src/NatureProtector.Infrastructure.Postgres/Migrations/`](../../src/NatureProtector.Infrastructure.Postgres/Migrations/)
- [`../../src/NatureProtector.Infrastructure.Influx/README.md`](../../src/NatureProtector.Infrastructure.Influx/README.md)
- [`../../src/NatureProtector.Infrastructure.Influx/Services/InfluxWriteService.cs`](../../src/NatureProtector.Infrastructure.Influx/Services/InfluxWriteService.cs)

O que procurar:

- que schemas existem em `PostgreSQL`;
- que dados são configuração, que dados são inbox e que dados são projeção operacional;
- que medições vão para `InfluxDB`.

### API

Lê:

- [`../../src/NatureProtector.Backoffice.Api/README.md`](../../src/NatureProtector.Backoffice.Api/README.md)
- [`../../src/NatureProtector.Backoffice.Api/Program.cs`](../../src/NatureProtector.Backoffice.Api/Program.cs)
- [`../../src/NatureProtector.Backoffice.Api/Controllers/ControlConfigurationsController.cs`](../../src/NatureProtector.Backoffice.Api/Controllers/ControlConfigurationsController.cs)
- [`../../src/NatureProtector.Backoffice.Api/Controllers/ControlAreasController.cs`](../../src/NatureProtector.Backoffice.Api/Controllers/ControlAreasController.cs)
- [`../../src/NatureProtector.Backoffice.Api/Controllers/ControlSimulationRunsController.cs`](../../src/NatureProtector.Backoffice.Api/Controllers/ControlSimulationRunsController.cs)
- [`../../src/NatureProtector.Backoffice.Api/ControlPlane/Services/PostgresControlPlaneService.cs`](../../src/NatureProtector.Backoffice.Api/ControlPlane/Services/PostgresControlPlaneService.cs)
- [`../../src/NatureProtector.Backoffice.Api/NatureProtector.Backoffice.Api.http`](../../src/NatureProtector.Backoffice.Api/NatureProtector.Backoffice.Api.http)

O que procurar:

- que endpoints já existem;
- que parte da configuração e do estado operacional já é consultável;
- como a API lê o `PostgreSQL` em vez de consultar estado em memória.

### Dados e manifests

Lê:

- [`../../data/README.md`](../../data/README.md)
- [`../../scripts/data/README.md`](../../scripts/data/README.md)
- [`../../data/baseline/areas/proenca-a-nova/manifest.json`](../../data/baseline/areas/proenca-a-nova/manifest.json)
- [`../../data/manifests/scenarios/proenca-a-nova-scenarios.generated.json`](../../data/manifests/scenarios/proenca-a-nova-scenarios.generated.json)
- [`../../data/manifests/scenarios/proenca-a-nova/scenario_b.high-risk.json`](../../data/manifests/scenarios/proenca-a-nova/scenario_b.high-risk.json)

O que procurar:

- diferença entre `external`, `baseline`, `manifests` e `runtime`;
- como a área piloto é materializada;
- como um cenário deixa de ser ideia e passa a ficheiro de definição executável.

### Scripts e infraestrutura

Lê:

- [`../../infra/README.md`](../../infra/README.md)
- [`../../infra/scripts/up.ps1`](../../infra/scripts/up.ps1)
- [`../../infra/scripts/smoke-test.ps1`](../../infra/scripts/smoke-test.ps1)
- [`../../infra/scripts/logs.ps1`](../../infra/scripts/logs.ps1)
- [`../../infra/scripts/down.ps1`](../../infra/scripts/down.ps1)
- [`../../scripts/dotnet/Use-RepoDotnetEnvironment.ps1`](../../scripts/dotnet/Use-RepoDotnetEnvironment.ps1)
- [`../../scripts/postgres/bootstrap-control-plane.ps1`](../../scripts/postgres/bootstrap-control-plane.ps1)
- [`../../docker-compose.yml`](../../docker-compose.yml)
- [`../../.env.example`](../../.env.example)

O que procurar:

- que baseline local existe;
- que variáveis e portas são assumidas;
- como o plano de controlo é carregado para `PostgreSQL`.

### Testes

Lê:

- [`../../tests/README.md`](../../tests/README.md)
- [`../../scripts/tests/generate-coverage-report.ps1`](../../scripts/tests/generate-coverage-report.ps1)

O que procurar:

- que módulos já têm melhor cobertura;
- onde ainda há zonas críticas sem testes significativos;
- que testes existem para integração entre simulador e prevenção.

## Ficheiros e hotspots que merecem leitura obrigatória

Se só puderes ler um conjunto curto de ficheiros, lê estes.

| Ficheiro | Porque vale a pena | O que deves procurar |
| --- | --- | --- |
| [`../../README.md`](../../README.md) | visão global real | hosts ativos, quickstart, modo com plano de controlo |
| [`architecture.md`](architecture.md) | narrativa técnica progressiva | cadeia completa do sistema |
| [`../../src/NatureProtector.Simulator.Host/Program.cs`](../../src/NatureProtector.Simulator.Host/Program.cs) | composição do simulador | escolha entre contexto local e contexto vindo de `PostgreSQL` |
| [`../../src/NatureProtector.Simulator.Host/Services/SimulationRunner.cs`](../../src/NatureProtector.Simulator.Host/Services/SimulationRunner.cs) | ciclo de vida da execução | seed, ciclos, `event_time`, publicação |
| [`../../src/NatureProtector.Simulator.Host/Services/ReadingGenerationService.cs`](../../src/NatureProtector.Simulator.Host/Services/ReadingGenerationService.cs) | modelo atual dos sensores virtuais | geração de temperatura, humidade e vento |
| [`../../src/NatureProtector.Simulator.Host/Services/PostgresSimulationContextSource.cs`](../../src/NatureProtector.Simulator.Host/Services/PostgresSimulationContextSource.cs) | ponte entre plano de controlo e simulador | leitura de área, cenário e sensores ativos |
| [`../../src/NatureProtector.Prevention.Host/Program.cs`](../../src/NatureProtector.Prevention.Host/Program.cs) | composição da pipeline | escolha entre persistência em memória e durável |
| [`../../src/NatureProtector.Prevention.Host/PreventionWorker.cs`](../../src/NatureProtector.Prevention.Host/PreventionWorker.cs) | entrada real da pipeline | consumo do broker, rejeição técnica, `ack` |
| [`../../src/NatureProtector.Prevention.Host/Processing/PostgresReadingEventInbox.cs`](../../src/NatureProtector.Prevention.Host/Processing/PostgresReadingEventInbox.cs) | coração da durabilidade | inbox, tentativas, retry e quarentena |
| [`../../src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs`](../../src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs) | efeito de negócio real | accepted reading, assessment, snapshot, projeção |
| [`../../src/NatureProtector.Infrastructure.Postgres/Bootstrap/ControlPlaneBootstrapper.cs`](../../src/NatureProtector.Infrastructure.Postgres/Bootstrap/ControlPlaneBootstrapper.cs) | carga inicial do plano de controlo | área piloto, grelha, sensores, cenários, datasets |
| [`../../src/NatureProtector.Backoffice.Api/Controllers/ControlAreasController.cs`](../../src/NatureProtector.Backoffice.Api/Controllers/ControlAreasController.cs) | superfície consultável do sistema | áreas, cenários, estado operacional e alertas |

## Como ligar a arquitetura ao código real

Uma forma segura de não te perderes é traduzir os blocos arquiteturais para os ficheiros que os implementam hoje.

| Bloco arquitetural | Ficheiros centrais |
| --- | --- |
| fontes e baseline de dados | [`../../data/README.md`](../../data/README.md), [`../../scripts/data/README.md`](../../scripts/data/README.md) |
| escolha e formalização de cenários | [`../../data/manifests/scenarios/proenca-a-nova-scenarios.generated.json`](../../data/manifests/scenarios/proenca-a-nova-scenarios.generated.json), [`../../src/NatureProtector.Simulator.Host/Configuration/GeneratedScenarioManifestLoader.cs`](../../src/NatureProtector.Simulator.Host/Configuration/GeneratedScenarioManifestLoader.cs) |
| construção do contexto da simulação | [`../../src/NatureProtector.Simulator.Host/Context/ScenarioContextFactory.cs`](../../src/NatureProtector.Simulator.Host/Context/ScenarioContextFactory.cs), [`../../src/NatureProtector.Simulator.Host/Services/PostgresSimulationContextSource.cs`](../../src/NatureProtector.Simulator.Host/Services/PostgresSimulationContextSource.cs) |
| simulador | [`../../src/NatureProtector.Simulator.Host/Services/SimulationRunner.cs`](../../src/NatureProtector.Simulator.Host/Services/SimulationRunner.cs), [`../../src/NatureProtector.Simulator.Host/Services/ReadingGenerationService.cs`](../../src/NatureProtector.Simulator.Host/Services/ReadingGenerationService.cs) |
| transporte por eventos | [`../../src/NatureProtector.Shared/Messaging/EventEnvelope.cs`](../../src/NatureProtector.Shared/Messaging/EventEnvelope.cs), [`../../src/NatureProtector.Shared/Messaging/NatureProtectorRabbitMqTopology.cs`](../../src/NatureProtector.Shared/Messaging/NatureProtectorRabbitMqTopology.cs) |
| pipeline de prevenção | [`../../src/NatureProtector.Prevention.Host/PreventionWorker.cs`](../../src/NatureProtector.Prevention.Host/PreventionWorker.cs), [`../../src/NatureProtector.Prevention.Host/Processing/ReadingEventProcessingService.cs`](../../src/NatureProtector.Prevention.Host/Processing/ReadingEventProcessingService.cs), [`../../src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs`](../../src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs) |
| persistência relacional | [`../../src/NatureProtector.Infrastructure.Postgres/Persistence/NatureProtectorControlDbContext.cs`](../../src/NatureProtector.Infrastructure.Postgres/Persistence/NatureProtectorControlDbContext.cs), [`../../src/NatureProtector.Infrastructure.Postgres/Migrations/`](../../src/NatureProtector.Infrastructure.Postgres/Migrations/) |
| persistência time-series | [`../../src/NatureProtector.Infrastructure.Influx/Services/InfluxWriteService.cs`](../../src/NatureProtector.Infrastructure.Influx/Services/InfluxWriteService.cs) |
| leitura operacional por HTTP | [`../../src/NatureProtector.Backoffice.Api/Program.cs`](../../src/NatureProtector.Backoffice.Api/Program.cs), [`../../src/NatureProtector.Backoffice.Api/Controllers/`](../../src/NatureProtector.Backoffice.Api/Controllers/) |

## Percurso recomendado para seguir um cenário end-to-end

Se quiseres perceber o sistema de ponta a ponta, segue este percurso.

1. Abre [`../../data/manifests/scenarios/proenca-a-nova/scenario_b.high-risk.json`](../../data/manifests/scenarios/proenca-a-nova/scenario_b.high-risk.json).  
   Procura: parâmetros do cenário e bloco `simulator_options`.
2. Lê [`../../scripts/postgres/bootstrap-control-plane.ps1`](../../scripts/postgres/bootstrap-control-plane.ps1) e depois [`../../src/NatureProtector.Postgres.Bootstrap/Program.cs`](../../src/NatureProtector.Postgres.Bootstrap/Program.cs).  
   Procura: como a baseline e os cenários são carregados para `PostgreSQL`.
3. Lê [`../../src/NatureProtector.Simulator.Host/Program.cs`](../../src/NatureProtector.Simulator.Host/Program.cs).  
   Procura: como o host escolhe o `PostgresSimulationContextSource`.
4. Lê [`../../src/NatureProtector.Simulator.Host/Services/PostgresSimulationContextSource.cs`](../../src/NatureProtector.Simulator.Host/Services/PostgresSimulationContextSource.cs).  
   Procura: resolução de área, cenário e sensores.
5. Lê [`../../src/NatureProtector.Simulator.Host/Services/SimulationRunner.cs`](../../src/NatureProtector.Simulator.Host/Services/SimulationRunner.cs).  
   Procura: seed, `SimulationRun`, ciclos e publicação.
6. Lê [`../../src/NatureProtector.Simulator.Host/Services/ReadingGenerationService.cs`](../../src/NatureProtector.Simulator.Host/Services/ReadingGenerationService.cs).  
   Procura: como nasce um `SensorReadingProduced`.
7. Lê [`../../src/NatureProtector.Prevention.Host/PreventionWorker.cs`](../../src/NatureProtector.Prevention.Host/PreventionWorker.cs).  
   Procura: consumo, rejeição técnica e armazenamento na inbox.
8. Lê [`../../src/NatureProtector.Prevention.Host/Processing/PostgresReadingEventInbox.cs`](../../src/NatureProtector.Prevention.Host/Processing/PostgresReadingEventInbox.cs) e [`../../src/NatureProtector.Prevention.Host/Processing/ReadingEventProcessingService.cs`](../../src/NatureProtector.Prevention.Host/Processing/ReadingEventProcessingService.cs).  
   Procura: retries, quarentena e idempotência.
9. Lê [`../../src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs`](../../src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs).  
   Procura: accepted reading, assessment, snapshot e estado operacional.
10. Fecha o percurso com [`../../src/NatureProtector.Backoffice.Api/Controllers/ControlAreasController.cs`](../../src/NatureProtector.Backoffice.Api/Controllers/ControlAreasController.cs) e [`../../src/NatureProtector.Backoffice.Api/Controllers/ControlSimulationRunsController.cs`](../../src/NatureProtector.Backoffice.Api/Controllers/ControlSimulationRunsController.cs).  
    Procura: o que já está consultável por HTTP.

## Erros de leitura comuns ou formas de não nos perdermos

- Não assumir que tudo o que está em `docs/planning/` já existe em runtime.
- Não confundir `data/external/` com a camada canónica do sistema; a leitura certa da baseline está em `data/baseline/`.
- Não começar pelo diagrama de domínio detalhado ou pelas migrations; isso é leitura de aprofundamento, não de arranque.
- Não assumir que `NatureProtector.Shared` é apenas utilitários genéricos; hoje ele fixa contratos e topologia de eventos.
- Não olhar para `Simulator.Host` e `Prevention.Host` como módulos de domínio; eles são hosts de composição e runtime.
- Não esquecer as flags `ControlPlaneEnabled` e `PipelinePersistenceEnabled`; elas mudam o caminho real de execução.

## Leituras seguintes para aprofundar

- [`current-capabilities-and-how-to-run.md`](current-capabilities-and-how-to-run.md) para saber o que já se consegue correr e observar hoje.
- [`architecture.md`](architecture.md) para a explicação arquitetural completa.
- [`../planning/project-completion-roadmap.md`](../planning/project-completion-roadmap.md) para perceber o que ainda é evolução futura.
- [`../../scripts/data/README.md`](../../scripts/data/README.md) para repetir ou alterar a pipeline de dados.
- [`../../tests/README.md`](../../tests/README.md) para perceber o estado da cobertura e dos testes de integração.
