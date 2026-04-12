# Plano de Conclusão do Projeto NatureProtector

## 1. Finalidade

Este documento transforma as decisões arquiteturais atuais, os resultados da investigação e o estado do repositório num plano para concluir a fase atual do NatureProtector.

É intencionalmente prático:

- define o que deve ser feito primeiro e porquê;
- identifica o que já está fechado pelos documentos do projeto;
- mapeia o trabalho alvo para o repositório atual;
- fornece um backlog concreto para implementação.

## 2. Fonte de Verdade

A fase atual deve ser guiada por estas referências, por esta ordem:

1. `NP_DocumentoDeFechoDoEscopo.pdf`
2. `pesquisa_incendios.pdf`
3. `ppG49.pdf`

Os dois primeiros documentos fecham o escopo presente. Os documentos de proposta mais antigos continuam a ser úteis para a visão da plataforma, para a linguagem comum do core partilhado e para a modularidade a longo prazo, mas já não definem, por si só, a ordem de implementação atual.

## 3. Decisões Já Fechadas

Estas decisões devem agora ser tratadas como fixas, exceto se uma ADR deliberada as alterar.

### 3.1 Decisões arquiteturais

- `PostgreSQL` é a fonte de verdade para os dados do plano de controlo.
- `InfluxDB` armazena a telemetria operacional e o histórico de séries temporais.
- `RabbitMQ` é o mecanismo de transporte de eventos e de desacoplamento.
- O plano de controlo e o plano de execução são preocupações separadas.
- Os eventos usam um envelope comum com, pelo menos:
  - `schema_version`
  - `event_id`
  - `correlation_id`
  - `producer`
  - `area_id`
  - `event_time`
  - `ingest_time`, quando aplicável
  - `payload`
- A pipeline deve tolerar duplicados e reentregas através de `event_id` e da lógica da aplicação.
- A persistência está distribuída por vários pontos do fluxo:
  - configuração de controlo
  - telemetria aceite
  - avaliações de risco
  - alertas e projeções

### 3.2 Decisões de simulação

- O simulador faz parte da fase atual, não é um extra futuro.
- O simulador deve suportar, pelo menos, três cenários:
  - `Cenário A`: dia normal de início de verão
  - `Cenário B`: dia de verão com perigo elevado/extremo
  - `Cenário C`: versão degradada de um cenário fisicamente plausível, com falhas de medição e da pipeline
- A simulação deve privilegiar um modelo contínuo, não valores isolados de execução única.
- O simulador deve separar:
  - geração da verdade física
  - erro de medição dos sensores
  - falha de comunicação/pipeline
- Os datasets, fórmulas e limiares devem ser versionados e rastreáveis.

## 4. Fotografia Atual do Repositório

### 4.1 Projetos existentes

- `src/NatureProtector.Core`
- `src/NatureProtector.Shared`
- `src/NatureProtector.Prevention`
- `src/NatureProtector.Prevention.Host`
- `src/NatureProtector.Simulator.Host`
- `src/NatureProtector.Infrastructure.Influx`
- `src/NatureProtector.Backoffice.Api`

### 4.2 Pontos de entrada atuais relevantes e hotspots

- composição atual do simulator host:
  - `src/NatureProtector.Simulator.Host/Program.cs`
- composição atual do prevention host:
  - `src/NatureProtector.Prevention.Host/Program.cs`
- pipeline de risco atual:
  - `src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs`
- contratos atuais e topologia RabbitMQ:
  - `src/NatureProtector.Shared/Contracts/*`
  - `src/NatureProtector.Shared/Messaging/*`
  - `src/NatureProtector.Shared/Configuration/RabbitMqOptions.cs`
- esqueleto atual da API:
  - `src/NatureProtector.Backoffice.Api/Program.cs`
- código residual histórico do simulador que foi removido na limpeza do repositório:
  - `src/NatureProtector.Simulator.Host/ReadingIngestionWorker.cs`
  - `src/NatureProtector.Simulator.Host/Configuration/PreventionOptions.cs`
  - `src/NatureProtector.Simulator.Host/Presistence/*`
  - `src/NatureProtector.Simulator.Host/Validation/*`

### 4.3 Lacunas atuais face à fase alvo

- já existe integração de runtime com `PostgreSQL` para `control`, `pipeline` e uma vaga útil de `projection`;
- já existe baseline preparada em ficheiros e já existe documentação de datasets e manifests na codebase, mas ainda falta integrar formalmente todos os metadados dos datasets com o plano de controlo e com as `simulation_runs`;
- já existe inbox durável e store básica de idempotência, mas ainda falta fechar a semântica completa `accepted / rejected / normalized`;
- ainda não existe fluxo `accepted / rejected / normalized` como estados independentes da pipeline;
- a API já expõe a superfície principal do plano de controlo e do estado operacional básico, mas ainda não fecha o backoffice completo;
- o simulador existe, mas ainda não está na forma em camadas exigida pela investigação;
- a prevenção ainda combina persistência durável e alguns componentes auxiliares em memória;
- os testes já cresceram para `Prevention`, `Backoffice.Api`, `Simulator.Host` e integração, embora continuem mais fortes no `Core`.

### 4.4 Estado atual face ao roadmap

Leitura honesta do estado do projeto:

- `Fase 0`: parcial. Já existe uma camada documental útil para navegação e leitura técnica, mas ainda faltam alguns artefactos-base pedidos pelo roadmap.
- `Fase 1`: em aberto. A modularização alvo ainda não foi executada.
- `Fase 2`: materialmente adiantada. O plano de controlo em `PostgreSQL` já existe em runtime para configuração, `simulation_runs`, inbox durável e primeira vaga de projeções.
- `Fase 3`: materialmente adiantada. A área piloto, a baseline preparada, os manifests e os cenários executáveis já existem.
- `Fase 4`: parcialmente adiantada. O simulador já usa seed determinística e consegue consumir manifests, mas ainda não está separado por camadas.

Isto significa que o projeto está num estado misto: as fases iniciais não ficaram totalmente fechadas, mas parte das fases de dados e cenários avançou antes do previsto. Esse desvio fez sentido na prática, porque ajudou a clarificar artefactos, cenários e a própria modelação das tabelas do plano de controlo.

## 5. Objetivos de Entrega da Fase Atual

O projeto só deve ser considerado concluído para esta fase quando todas as condições seguintes forem verdadeiras:

1. a estrutura do repositório reflete claramente a arquitetura pretendida;
2. a configuração do plano de controlo é durável, versionada e gerida através de `PostgreSQL`;
3. os datasets estão catalogados, versionados e associados a cenários de forma reprodutível;
4. o simulador produz outputs fisicamente plausíveis, rastreáveis e configuráveis;
5. a pipeline de execução é durável, auditável e tolerante à entrega duplicada;
6. o risco é calculado a partir de dados aceites e normalizados, não diretamente a partir de mensagens raw;
7. os alertas e as projeções são etapas explícitas, não efeitos secundários implícitos;
8. o sistema expõe observabilidade suficiente para demonstrar o fluxo end-to-end.

## 6. Regra de Ordenação do Trabalho

O projeto não deve continuar a avançar como se bastasse "terminar a pipeline" em cima da forma atual do repositório.

Na prática, o repositório já adiantou parte das fases de datasets e cenários antes de fechar a base arquitetural. Esse desvio foi útil porque reduziu a incerteza sobre artefactos, regras e necessidades do simulador. A partir deste ponto, a ordem recomendada passa a ser:

1. fechar a baseline técnica em falta;
2. corrigir a estrutura do repositório mínima que bloqueia a separação de responsabilidades;
3. implementar o plano de controlo em `PostgreSQL`;
4. consolidar o catálogo de datasets e as associações a cenários dentro do plano de controlo;
5. implementar o simulador em camadas;
6. implementar ingestão durável e normalização;
7. implementar risco, alertas e projeções;
8. completar API, dashboards e testes.

Esta ordem minimiza refatorações destrutivas a partir do estado real em que o projeto hoje se encontra.

## 7. Estrutura Alvo do Repositório

A estrutura alvo recomendada é intencionalmente modular, mas continua suficientemente compacta para o escopo atual.

```text
src/
  NatureProtector.Core/
  NatureProtector.Contracts/
  NatureProtector.Simulation/
  NatureProtector.Pipeline/
  NatureProtector.Prevention/
  NatureProtector.Infrastructure.Postgres/
  NatureProtector.Infrastructure.RabbitMq/
  NatureProtector.Infrastructure.Influx/
  NatureProtector.Backoffice.Api/
  NatureProtector.Simulator.Host/
  NatureProtector.Prevention.Host/

tests/
  NatureProtector.Core.Tests/
  NatureProtector.Contracts.Tests/
  NatureProtector.Simulation.Tests/
  NatureProtector.Pipeline.Tests/
  NatureProtector.Prevention.Tests/
  NatureProtector.IntegrationTests/

docs/
  architecture/
  contracts/
  planning/
  simulation/
  decisions/

data/
  baseline/
  manifests/
  runtime/
  external/
```

### 7.1 Responsabilidades dos projetos

#### `NatureProtector.Core`

- conceitos de área, grelha, sensor, cenário, regra, risco e alerta que pertencem ao domínio;
- nenhum comportamento específico de broker, armazenamento ou HTTP.

#### `NatureProtector.Contracts`

- envelope de evento;
- tipos de evento;
- contratos de payload;
- DTOs públicos partilhados entre hosts e API.

#### `NatureProtector.Simulation`

- modelo de verdade física;
- modelo de erro do sensor;
- modelo de falha de transporte;
- executor de cenários;
- modelo de configuração da simulação.

#### `NatureProtector.Pipeline`

- ingestão;
- validação;
- normalização;
- idempotência;
- máquina de estados da inbox;
- quality flags;
- classificação de retries.

#### `NatureProtector.Prevention`

- modelo de risco;
- agregação;
- política de alertas;
- geração de recomendações;
- construção de projeções.

#### `NatureProtector.Infrastructure.Postgres`

- implementação em EF Core ou em repositório para tabelas do plano de controlo e do estado de execução;
- migrations;
- persistência do catálogo de datasets;
- persistência da inbox;
- persistência do estado de alertas e das projeções.

#### `NatureProtector.Infrastructure.RabbitMq`

- opções de `RabbitMQ`;
- topologia;
- abstrações e implementações de publishers;
- abstrações de consumers, quando úteis.

#### `NatureProtector.Infrastructure.Influx`

- caminho de escrita da telemetria aceite;
- caminho de escrita da telemetria normalizada ou derivada;
- persistência em séries temporais de risco e alertas.

#### `NatureProtector.Backoffice.Api`

- endpoints de configuração;
- endpoints de ativação de cenários;
- endpoints do catálogo de datasets;
- endpoints de consulta para projeções e histórico.

#### `NatureProtector.Simulator.Host`

- apenas arranque do host e orquestração;
- consome `Simulation` + `Contracts` + `Infrastructure.RabbitMq`.

#### `NatureProtector.Prevention.Host`

- apenas arranque do host e orquestração;
- consome `Pipeline` + `Prevention` + projetos de infraestrutura.

## 8. Projetos a Criar, Mover e Remover

### 8.1 Criar

- `src/NatureProtector.Contracts`
- `src/NatureProtector.Simulation`
- `src/NatureProtector.Pipeline`
- `src/NatureProtector.Infrastructure.Postgres`
- `src/NatureProtector.Infrastructure.RabbitMq`
- `tests/NatureProtector.Contracts.Tests`
- `tests/NatureProtector.Simulation.Tests`
- `tests/NatureProtector.Pipeline.Tests`
- `tests/NatureProtector.IntegrationTests`

### 8.2 Mover a partir dos projetos atuais

#### De `NatureProtector.Shared`

Mover para `NatureProtector.Contracts`:

- `Contracts/Readings/*`
- `Messaging/EventEnvelope.cs`
- `Messaging/EventTypes.cs`
- `Messaging/JsonEventSerializer.cs`

Mover para `NatureProtector.Infrastructure.RabbitMq`:

- `Configuration/RabbitMqOptions.cs`
- `Messaging/NatureProtectorRabbitMqTopology.cs`
- `Messaging/RoutingKeys.cs`

#### De `NatureProtector.Simulator.Host`

Mover para `NatureProtector.Simulation`:

- `Context/ScenarioContextFactory.cs`
- `Context/SimulationContext.cs`
- `Services/SeedProvider.cs`
- `Services/ReadingGenerationService.cs`
- `Configuration/SimulatorOptions.cs`

Mover para `NatureProtector.Pipeline` ou apagar após substituição:

- `ReadingIngestionWorker.cs` `resolved`
- `Configuration/PreventionOptions.cs` `resolved`
- `Presistence/*` `resolved`
- `Validation/*` `resolved`

Manter em `NatureProtector.Simulator.Host`:

- `Program.cs`
- peças de orquestração exclusivas do host
- seleção ou ligação do publisher após refatoração

#### De `NatureProtector.Prevention.Host`

Mover para `NatureProtector.Pipeline`:

- orquestração de processamento accepted/rejected/normalized
- ingestion workers
- políticas de validação e normalização

Manter em `NatureProtector.Prevention.Host`:

- bootstrap do host
- composição dos workers

#### De `NatureProtector.Prevention`

Manter e expandir:

- scoring de risco
- agregação de risco
- política de alertas
- recomendações
- projeções

### 8.3 Remover após migração

- `src/NatureProtector.Shared` como projeto balde de longo prazo;
- resíduo de ingestão do lado do simulador em `NatureProtector.Simulator.Host` `resolved`;
- pastas `Presistence` mal escritas após a migração `resolved`.

## 9. Estratégia de Armazenamento de Datasets

Os datasets devem ser divididos em três camadas.

### 9.1 Camada de ficheiros

Os artefactos reais dos datasets vivem no sistema de ficheiros, não em `PostgreSQL` nem em `Influx`.

Estrutura recomendada:

```text
data/
  baseline/
    areas/
      proenca-a-nova/
        area.gpkg
        grid_1km.gpkg
        cells_attributes.parquet
        weather_reference.parquet
        weather_daily_reference.parquet
        fire_history.parquet
        scenario_candidates.parquet
        manifest.json
      covilha/
      monchique/
  manifests/
    datasets/
    scenarios/
  external/
    ipma/
    era5-land/
    cems-effis/
    icnf/
    corine/
  runtime/
    simulations/
    exports/
```

Regras:

- `baseline/` armazena artefactos pequenos e preparados que definem o input canónico da demonstração;
- `external/` armazena inputs raw descarregados ou importados e deve, normalmente, estar em `.gitignore`;
- `runtime/` armazena outputs gerados e deve estar em `.gitignore`;
- cada dataset preparado de área recebe um `manifest.json` com origem, versão, checksum e data.

### 9.2 Camada de metadados em PostgreSQL

`PostgreSQL` armazena metadados e associações, não os datasets raw completos de séries temporais.

Deve responder a:

- que versão de dataset foi usada;
- que cenário foi associado a que artefactos de dataset;
- que fórmulas e limiares estavam ativos;
- que execution run usou essa combinação exata.

### 9.3 Camada operacional

`InfluxDB` armazena:

- leituras aceites;
- leituras normalizadas, se forem retidas como measurement;
- séries de risco;
- séries de alertas;
- métricas de observabilidade.

## 10. Tabelas PostgreSQL

Devemos usar schemas para tornar a separação explícita.

### 10.1 Schema `control`

#### `control.configuration_versions`

- `id`
- `version_number`
- `description`
- `is_active`
- `created_at`
- `created_by`

#### `control.areas`

- `id`
- `code`
- `name`
- `country_code`
- `geometry_geojson`
- `metadata_json`
- `configuration_version_id`

#### `control.grid_cells`

- `id`
- `area_id`
- `cell_code`
- `centroid_latitude`
- `centroid_longitude`
- `polygon_geojson`
- `altitude_m`
- `slope_deg`
- `aspect_deg`
- `land_cover_class`
- `dominant_forest_type`
- `dominant_fuel_model`
- `tree_cover_density`
- `structural_hazard`
- `conjunctural_hazard`
- `configuration_version_id`

#### `control.sensor_profiles`

- `id`
- `name`
- `sensor_family`
- `accuracy_profile_json`
- `noise_profile_json`
- `fault_profile_json`
- `publication_policy_json`
- `configuration_version_id`

#### `control.sensors`

- `id`
- `area_id`
- `grid_cell_id`
- `profile_id`
- `name`
- `type`
- `latitude`
- `longitude`
- `altitude_m`
- `is_active`
- `installation_profile`
- `configuration_version_id`

#### `control.scenarios`

- `id`
- `code`
- `name`
- `scenario_kind`
- `description`
- `base_scenario_id`
- `parameters_json`
- `configuration_version_id`

#### `control.rule_set_versions`

- `id`
- `name`
- `version`
- `description`
- `parameters_json`
- `is_active`
- `configuration_version_id`

#### `control.dataset_artifacts`

- `id`
- `dataset_code`
- `dataset_type`
- `source_name`
- `source_url`
- `area_code`
- `version`
- `format`
- `relative_path`
- `checksum`
- `valid_from`
- `valid_to`
- `metadata_json`

#### `control.scenario_dataset_bindings`

- `id`
- `scenario_id`
- `dataset_artifact_id`
- `binding_role`
- `notes`

### 10.2 Schema `execution`

#### `execution.simulation_runs`

- `id`
- `scenario_id`
- `configuration_version_id`
- `rule_set_version_id`
- `seed`
- `started_at`
- `completed_at`
- `status`
- `dataset_snapshot_json`

#### `execution.event_inbox`

- `id`
- `event_id`
- `correlation_id`
- `event_type`
- `producer`
- `area_id`
- `event_time`
- `ingest_time`
- `payload_hash`
- `raw_envelope_json`
- `processing_status`
- `attempt_count`
- `first_seen_at`
- `last_attempt_at`
- `last_error_code`
- `last_error_stage`
- `last_error_message`

#### `execution.event_processing_attempts`

- `id`
- `event_inbox_id`
- `attempt_number`
- `started_at`
- `finished_at`
- `result`
- `stage`
- `error_code`
- `error_message`

#### `execution.alert_state`

- `id`
- `area_id`
- `grid_cell_id`
- `alert_type`
- `severity`
- `state`
- `opened_at`
- `updated_at`
- `closed_at`
- `source_event_id`
- `source_risk_event_id`
- `justification_json`

#### `execution.operational_projections`

- `id`
- `projection_key`
- `projection_type`
- `area_id`
- `grid_cell_id`
- `payload_json`
- `updated_at`
- `source_event_id`

### 10.3 O que não deve ser uma tabela PostgreSQL de primeira onda

Não devemos começar por construir um warehouse totalmente normalizado para cada leitura e métrica.

Não na primeira onda:

- espelho relacional completo da telemetria raw do `Influx`;
- tabelas avançadas de `RBAC`, exceto se a API precisar delas agora;
- event store histórico completo de projeções, se as projeções atuais puderem ser reconstruídas.

## 11. Catálogo Inicial de Eventos

### 11.1 Envelope

Todos os eventos devem usar o envelope comum:

- `schema_version`
- `event_id`
- `correlation_id`
- `producer`
- `event_type`
- `area_id`
- `event_time`
- `ingest_time`
- `payload`

### 11.2 Conjunto inicial de eventos

| Evento | Producer | Principais Consumers | Notas |
| --- | --- | --- | --- |
| `ConfigChanged` | Backoffice / plano de controlo | gestão de cenários, hosts, auditoria | refresh do plano de controlo |
| `ScenarioActivated` | gestão de cenários | simulador, observabilidade, API | inicia uma run |
| `ScenarioStopped` | gestão de cenários | simulador, observabilidade, API | termina uma run |
| `SensorNetworkInstantiated` | gestão de cenários | simulador, observabilidade | rede de execução resolvida |
| `SensorReadingProduced` | simulação | ingestão da pipeline, observabilidade | obrigatório |
| `SensorBatchProduced` | simulação | ingestão da pipeline | opcional, apenas se o batching for real |
| `SensorFaultRaised` | simulação | pipeline, observabilidade, API | obrigatório para cenários degradados |
| `ReadingAccepted` | pipeline | influx, prevention, observabilidade | aceite estrutural ou semanticamente |
| `ReadingRejected` | pipeline | observabilidade, auditoria, armazenamento opcional | evento rejeitado |
| `ReadingNormalized` | pipeline | prevention, persistência | input canónico para o risco |
| `RiskEvaluated` | prevention | alertas, persistência, API | por célula ou orientado à leitura |
| `AreaRiskAggregated` | prevention | alertas, persistência, API | por área |
| `WarningRaised` | alertas | persistência, API, observabilidade | caminho de warning |
| `AlarmRaised` | alertas | persistência, API, observabilidade | caminho de alarm |
| `RecommendationGenerated` | alertas | persistência, API | recomendação concisa |
| `ProjectionUpdated` | projeções ou persistência | API | vista operacional atualizada |

## 12. Roadmap por Fase

## Fase 0, Congelar a Baseline Técnica

### Objetivo

Transformar as decisões atuais em contratos de implementação.

### Estado atual

Parcial. Já existe uma camada documental transversal e local suficiente para navegação, mas ainda faltam os artefactos formais desta fase.

### Referências do repositório

- diagrama atual de arquitetura:
  - `docs/architecture/natureprotector-current-architecture.drawio.xml`
- simulator host atual:
  - `src/NatureProtector.Simulator.Host/Program.cs`
- prevention host atual:
  - `src/NatureProtector.Prevention.Host/Program.cs`

### Tarefas

- criar `docs/contracts/event-catalog.md`;
- criar `docs/architecture/module-baseline.md`;
- criar `docs/simulation/simulation-spec.md`;
- aprovar a estrutura alvo dos projetos;
- aprovar a lista de tabelas `PostgreSQL`;
- aprovar a estratégia de pastas de datasets.

### Critérios de saída

- não existe ambiguidade em aberto sobre onde vivem a configuração, os datasets, o estado de runtime e a telemetria.

## Fase 1, Limpeza do Repositório e Extração Modular

### Objetivo

Fazer com que a estrutura do repositório reflita a arquitetura antes de acrescentarmos mais comportamento.

### Estado atual

Em aberto. A estrutura alvo já está identificada, mas ainda não foi implementada.

### Tarefas

- criar novos projetos:
  - `NatureProtector.Contracts`
  - `NatureProtector.Simulation`
  - `NatureProtector.Pipeline`
  - `NatureProtector.Infrastructure.Postgres`
  - `NatureProtector.Infrastructure.RabbitMq`
- mover contratos e topologia `RabbitMQ` para fora de `NatureProtector.Shared`;
- mover serviços de simulação para fora de `NatureProtector.Simulator.Host`;
- remover código residual de ingestão do lado do simulador `resolved`;
- renomear pastas `Presistence` mal escritas para `Persistence` `resolved`;
- reduzir os hosts a projetos apenas de composição.

### Critérios de saída

- não permanece lógica de ingestão dentro do projeto do simulador;
- os hosts são finos e focados em orquestração;
- os contratos partilhados estão separados da ligação à infraestrutura.

## Fase 2, Plano de Controlo em PostgreSQL

### Objetivo

Implementar a fonte de verdade exigida pelo documento de escopo.

### Estado atual

Em aberto. O desenho das tabelas já está muito mais claro, mas ainda não existe implementação de runtime.

### Tarefas

- introduzir migrations da base de dados;
- implementar o schema `control`;
- semear uma área piloto;
- semear uma versão de configuração;
- semear uma primeira versão de rule set;
- expor a configuração primeiro através de serviços internos, depois através da API.

### Critérios de saída

- simulador e prevenção conseguem resolver a configuração ativa a partir de `PostgreSQL`.

## Fase 3, Catálogo de Datasets e Inputs de Baseline

### Objetivo

Definir que datasets existem, onde vivem e como as runs se referem a eles.

### Estado atual

Materialmente adiantada. A baseline da área piloto, os manifests e os cenários executáveis já existem em ficheiros; falta a integração formal com `PostgreSQL` e o rastreio das runs.

### Tarefas

- consolidar `data/baseline/areas/<pilot-area>/`;
- consolidar manifests por área;
- carregar metadados de artefactos de datasets para `PostgreSQL`;
- criar registos de associação cenário para dataset;
- fixar convenções de ficheiros para:
  - limite da área
  - grelha
  - atributos das células
  - referência meteorológica
  - histórico de incêndios
  - candidatos a cenário

### Critérios de saída

- uma simulation run pode ser rastreada até aos artefactos exatos de dataset.

## Fase 4, Motor de Simulação

### Objetivo

Construir o simulador na forma em camadas exigida pela investigação.

### Estado atual

Parcialmente adiantada. Já existe execução determinística, manifests e cenários de base, mas falta a separação explícita entre verdade física, erro de sensor e falha de transporte.

### Tarefas

- definir `IPhysicalScenarioModel`;
- definir `ISensorErrorModel`;
- definir `ITransportFaultModel`;
- implementar o modelo físico de baseline;
- implementar a aplicação de perfis de sensores;
- implementar o `Cenário A`;
- implementar o `Cenário B`;
- implementar o `Cenário C` como variante degradada de `A` ou `B`;
- suportar execução determinística baseada em seed;
- associar cada run a um registo em `simulation_runs`.

### Critérios de saída

- o simulador produz outputs determinísticos a partir da mesma seed e da mesma associação de cenário;
- o `Cenário C` preserva a mesma verdade física da sua base limpa.

## Fase 5, Pipeline Durável

### Objetivo

Substituir o caminho atual de execução em memória por um fluxo durável e idempotente.

### Tarefas

- implementar persistência da inbox em `PostgreSQL`;
- classificar erros em:
  - contrato permanente
  - domínio permanente
  - infraestrutura transitória
  - desconhecido
- implementar pipeline de validação e normalização;
- emitir:
  - `ReadingAccepted`
  - `ReadingRejected`
  - `ReadingNormalized`
- armazenar tentativas de processamento;
- deduplicar por `event_id`;
- basear a ordenação e a reconstrução em `event_time`.

### Critérios de saída

- a duplicação de mensagens não cria efeitos de negócio duplicados;
- aceitação, rejeição e normalização são estados explícitos.

## Fase 6, Prevenção, Alertas e Projeções

### Objetivo

Calcular outputs operacionais apenas a partir de dados normalizados.

### Tarefas

- adaptar a prevenção para consumir apenas leituras normalizadas;
- calcular risco por célula;
- agregar risco por área;
- preservar a explicação e os fatores dominantes;
- implementar política de alertas com:
  - severidade
  - histerese
  - cooldown
  - justificação
- implementar projeções operacionais para a UI.

### Critérios de saída

- risco, warning e alarm são estados distintos com outputs explícitos.

## Fase 7, API e Superfície de Consulta

### Objetivo

Expor uma interface estável de controlo e de consulta.

### Tarefas

- adicionar endpoints de configuração;
- adicionar endpoints de ativação de cenários;
- adicionar endpoints do catálogo de datasets;
- adicionar endpoints de consulta de projeções;
- adicionar endpoints de histórico de runs.

### Critérios de saída

- a UI deixa de depender de estado raw de runtime ou de repositórios em memória.

## Fase 8, Observabilidade e Validação

### Objetivo

Tornar o sistema demonstrável, diagnosticável e testável.

### Tarefas

- adicionar dashboards para:
  - throughput
  - backlog
  - latência
  - contagens de accepted/rejected
  - supressão de duplicados
  - contagens de retry/quarantine
  - risco por área
  - alertas ativos
- adicionar testes para:
  - contratos
  - simulação
  - pipeline
  - prevenção
  - integração
- documentar proveniência e outputs de validação.

### Critérios de saída

- o projeto pode ser demonstrado end-to-end com evidência, não apenas com logs.

## 13. Backlog de Implementação Ordenado

O backlog abaixo mantém a ordem alvo. Vários itens da frente `DATA` já foram adiantados no repositório e passam agora de criação inicial para consolidação e integração.

### ARCH

- `ARCH-01` Criar novos projetos e atualizar a solution.
- `ARCH-02` Dividir `NatureProtector.Shared` em `Contracts` e `Infrastructure.RabbitMq`.
- `ARCH-03` Extrair serviços de simulação para `NatureProtector.Simulation`.
- `ARCH-04` Remover código residual de ingestão do simulador.
- `ARCH-05` Renomear ou limpar pastas e namespaces de persistência.

### DATA

- `DATA-01` Consolidar `data/baseline`, `data/manifests`, `data/external`, `data/runtime`.
- `DATA-02` Formalizar o schema de `manifest.json` para artefactos de dataset.
- `DATA-03` Consolidar os ficheiros baseline da área piloto.
- `DATA-04` Registar artefactos de dataset da área piloto em `PostgreSQL`.
- `DATA-05` Definir associações cenário para dataset.

### PG

- `PG-01` Criar `NatureProtector.Infrastructure.Postgres`.
- `PG-02` Adicionar migrations e configuração base de ligação.
- `PG-03` Implementar `control.configuration_versions`.
- `PG-04` Implementar `control.areas` e `control.grid_cells`.
- `PG-05` Implementar `control.sensor_profiles` e `control.sensors`.
- `PG-06` Implementar `control.scenarios`.
- `PG-07` Implementar `control.rule_set_versions`.
- `PG-08` Implementar `control.dataset_artifacts`.
- `PG-09` Implementar `control.scenario_dataset_bindings`.
- `PG-10` Implementar `execution.simulation_runs`.
- `PG-11` Implementar `execution.event_inbox`.
- `PG-12` Implementar `execution.event_processing_attempts`.
- `PG-13` Implementar `execution.alert_state`.
- `PG-14` Implementar `execution.operational_projections`.

### SIM

- `SIM-01` Definir contratos e settings da simulação.
- `SIM-02` Implementar o modelo de verdade física.
- `SIM-03` Implementar perfis de erro dos sensores.
- `SIM-04` Implementar perfis de falha da pipeline.
- `SIM-05` Implementar o executor de cenários e os metadados da run.
- `SIM-06` Publicar `SensorReadingProduced`.
- `SIM-07` Publicar `SensorFaultRaised`.

### PIPE

- `PIPE-01` Criar `NatureProtector.Pipeline`.
- `PIPE-02` Implementar validação do envelope.
- `PIPE-03` Implementar validação semântica.
- `PIPE-04` Implementar normalização.
- `PIPE-05` Implementar verificação de idempotência por `event_id`.
- `PIPE-06` Implementar persistência da inbox e logging de tentativas.
- `PIPE-07` Emitir `ReadingAccepted`.
- `PIPE-08` Emitir `ReadingRejected`.
- `PIPE-09` Emitir `ReadingNormalized`.

### PREV

- `PREV-01` Refatorar a prevenção para consumir apenas dados normalizados.
- `PREV-02` Implementar risco por célula.
- `PREV-03` Implementar agregação por área.
- `PREV-04` Implementar payloads de explicabilidade.
- `PREV-05` Implementar política de warning.
- `PREV-06` Implementar política de alarm.
- `PREV-07` Implementar geração de recomendações.
- `PREV-08` Construir projeções operacionais.

### API

- `API-01` Adicionar endpoints de leitura ou escrita de configuração.
- `API-02` Adicionar endpoints de ativação de cenários.
- `API-03` Adicionar endpoints do catálogo de datasets.
- `API-04` Adicionar endpoints de projeções.
- `API-05` Adicionar endpoints de simulation run.

### OBS

- `OBS-01` Substituir o dashboard Grafana apenas de setup por painéis reais.
- `OBS-02` Rastrear supressão de duplicados.
- `OBS-03` Rastrear contadores accepted/rejected/normalized.
- `OBS-04` Rastrear metadados de simulação ao nível da run.

### TEST

- `TEST-01` Adicionar testes de `Contracts`.
- `TEST-02` Adicionar testes de `Simulation`.
- `TEST-03` Adicionar testes de `Pipeline`.
- `TEST-04` Preencher `Prevention.Tests`.
- `TEST-05` Adicionar testes de integração end-to-end.

## 14. Primeiro Marco Recomendado

O melhor primeiro marco já não é "criar datasets do zero", porque essa frente foi parcialmente adiantada.

É:

- baseline de dataset da área piloto consolidada e referenciada;
- repositório limpo e modularizado o suficiente para separar responsabilidades;
- plano de controlo em `PostgreSQL` a funcionar;
- ativação de cenário persistida;
- simulador associado a uma configuração real e a um dataset manifest.

Só depois deste marco é que a equipa deve avançar para ingestão durável e para o fluxo completo de prevenção.

## 15. Definition of Done para a Fase Atual

A fase está concluída quando o projeto consegue demonstrar:

1. uma área piloto e uma grelha configuradas a partir de `PostgreSQL`;
2. um cenário associado a artefactos de dataset versionados;
3. uma simulation run determinística;
4. fluxo de eventos accepted/rejected/normalized com idempotência;
5. risco por célula e por área;
6. warnings e alarms com justificação;
7. projeções servidas à interface/API;
8. dashboards e logs que provam o comportamento end-to-end.
