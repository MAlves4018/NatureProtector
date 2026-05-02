# Plano de Conclusão do Projeto NatureProtector

## 1. Finalidade

Este documento transforma as decisões arquiteturais atuais, os resultados da investigação, o estado real do repositório e a validação técnica recente num plano de conclusão para a fase atual do NatureProtector.

O objetivo não é apenas definir uma arquitetura alvo. O objetivo é orientar a fase atual com foco operacional, preservando simultaneamente a visão estratégica do projeto.

Este documento é intencionalmente prático:

* define o que deve ser feito primeiro e porquê;
* distingue o que já está implementado, o que está parcial, o que é experimental e o que está dependente de pesquisa;
* preserva as decisões arquiteturais e metodológicas já estabilizadas;
* mapeia o trabalho alvo para o repositório atual;
* define um próximo marco técnico claro;
* mantém um backlog concreto para implementação, testes, documentação e validação.

## 2. Fonte de Verdade Atual

Para a fase atual, o trabalho deve ser orientado pela documentação do repositório e pelos documentos referentes ao segundo momento de pesquisa.

Estes documentos consolidam o modelo metodológico, a especificação técnica da V1 e a tradução executável para artefactos, regras e decisões de implementação.

A fonte de verdade prática deve ser lida por esta ordem:

1. código e testes executáveis do repositório;
2. documentação de implementação em `docs/architecture/implementation.md` e restantes documentos arquiteturais do repositório;
3. documentos referentes ao segundo momento de pesquisa, quando presentes ou referenciados nesta branch;
4. este roadmap, enquanto guia operacional e estratégico da fase atual.

Isto implica duas regras:

* a pesquisa metodológica continua relevante, mas não deve ser tratada como totalmente fechada se essa conclusão não estiver explicitamente confirmada;
* a implementação corrente do repositório continua a ser o primeiro critério para declarar algo como implementado, parcial, experimental ou pendente.

Documentos de pesquisa e especificação a considerar nesta fase:

* `pesquisa_2_modelo_justificavel_nova.pdf`;
* `especificacao_implementacao_v1_natureprotector.pdf`;
* `suplemento_executavel_v1_natureprotector.pdf`;
* documentação arquitetural e operacional do repositório.

## 3. Decisões Já Fechadas

Estas decisões devem ser tratadas como fixas, exceto se uma ADR deliberada as alterar.

### 3.1 Decisões arquiteturais

* `PostgreSQL` é a fonte de verdade para os dados do plano de controlo, estado durável da pipeline e projeções operacionais.
* `InfluxDB` armazena telemetria operacional, séries temporais e informação útil para observabilidade.
* `RabbitMQ` é o mecanismo de transporte de eventos e de desacoplamento entre produtor e consumidor.
* O plano de controlo e o plano de execução são preocupações separadas.
* Os eventos usam um envelope comum com, pelo menos:

  * `schema_version`;
  * `event_id`;
  * `correlation_id`;
  * `producer`;
  * `event_type`;
  * `area_id`;
  * `event_time`;
  * `ingest_time`, quando aplicável;
  * `payload`.
* A pipeline deve tolerar duplicados e reentregas através de `event_id` e da lógica da aplicação.
* A persistência está distribuída por vários pontos do fluxo:

  * configuração de controlo;
  * inbox durável;
  * tentativas de processamento;
  * rejeições;
  * quarentena;
  * telemetria aceite;
  * avaliações de risco;
  * snapshots;
  * alertas;
  * projeções operacionais.

### 3.2 Decisões de simulação

* O simulador faz parte da fase atual, não é um extra futuro.
* O simulador deve suportar, pelo menos, três cenários:

  * `Cenário A`: dia normal de início de verão;
  * `Cenário B`: dia de verão com perigo elevado ou extremo;
  * `Cenário C`: versão degradada de um cenário fisicamente plausível, com falhas de medição e/ou da pipeline.
* A simulação deve privilegiar um modelo contínuo, não valores isolados de execução única.
* O simulador deve separar:

  * geração da verdade física;
  * erro de medição dos sensores;
  * falha de comunicação/pipeline.
* Os datasets, fórmulas, seeds, thresholds e configurações relevantes devem ser versionados e rastreáveis.
* O `Cenário C` deve preservar a mesma base física do cenário limpo e degradar apenas observação e/ou transporte.

### 3.3 Decisões de prevenção e pipeline

* A camada de risco não deve consumir diretamente envelopes crus.
* O fluxo alvo deve distinguir:

  * evento recebido;
  * evento tecnicamente válido;
  * evento rejeitado;
  * leitura aceite;
  * leitura normalizada;
  * leitura elegível para risco;
  * leitura excluída do risco;
  * evento reprocessável;
  * evento quarentenado.
* `accepted`, `rejected` e `normalized` devem tornar-se conceitos explícitos da pipeline.
* `RiskInput` deve funcionar como fronteira entre pipeline e prevenção.
* As projeções operacionais devem ser a superfície persistida e consultável para API/UI.

## 4. Estado Atual do Projeto

### 4.1 Já implementado

* baseline local com `RabbitMQ`, `PostgreSQL`, `InfluxDB` e `Grafana`;
* plano de controlo em `PostgreSQL`;
* bootstrap do plano de controlo;
* `Simulator.Host` ligado ao plano de controlo quando `ControlPlaneEnabled = true`;
* `SimulationRun` persistida em `control.simulation_runs`;
* `Prevention.Host` com inbox durável;
* retry interno e quarentena persistida;
* rejeição pré-inbox de eventos tecnicamente inválidos, incluindo `OperationalState = Invalid`;
* validação semântica sensor-área contra o plano de controlo antes da pipeline de risco;
* persistência de leituras aceites;
* persistência de avaliações de risco;
* persistência de snapshots de risco por área;
* persistência de projeções operacionais;
* `Backoffice.Api` de leitura sobre `control` e `projection`;
* observabilidade inicial com `OpenTelemetry`, `InfluxDB`, `Grafana` e documentação técnica complementar;
* modo local com `InfluxDb:Enabled=false`, writer `NoOp` e batch síncrono por evento quando `InfluxDB` está ativo;
* documentação de implementação em `docs/`;
* testes reforçados nos adaptadores PostgreSQL da `NatureProtector.Prevention.Host`;
* coverage consolidada recente:

  * global line coverage: cerca de `91%`;
  * `NatureProtector.Prevention.Host`: cerca de `91%`;
  * principais adaptadores PostgreSQL da pipeline com cobertura elevada.

### 4.2 Parcialmente implementado

* semântica completa de `accepted`, `rejected` e `normalized`;
* simulador em camadas `TruthSnapshot`, `LocalObservation` e `OperationalEvent`;
* score operacional final;
* alertas finais com histerese, cooldown, acknowledgement e justificação rica;
* agregação de área mais avançada;
* camada Aspire/AppHost para desenvolvimento local, ainda experimental.

### 4.3 Pendente ou dependente de pesquisa

* `FWI` final;
* `KBDI` final;
* `DailyCellState`;
* `RiskInput` final;
* score composto final;
* validação metodológica final dos cenários;
* casos canónicos completos;
* agregação espacial final;
* política final de alertas e recomendações;
* separação completa entre verdade física, observação local e falhas de transporte.

### 4.4 Experimental

* `src/NatureProtector.AppHost/`, enquanto camada de orquestração local/Aspire;
* eventual `webUI`, caso ainda não esteja integrada com o fluxo principal de validação e demonstração;
* modularização futura para projetos `Contracts`, `Simulation`, `Pipeline` e `Infrastructure.RabbitMq`.

## 5. Fotografia Atual do Repositório

### 5.1 Projetos existentes relevantes

* `src/NatureProtector.Core`
* `src/NatureProtector.Shared`
* `src/NatureProtector.Prevention`
* `src/NatureProtector.Prevention.Host`
* `src/NatureProtector.Simulator.Host`
* `src/NatureProtector.Infrastructure.Postgres`
* `src/NatureProtector.Infrastructure.Influx`
* `src/NatureProtector.Backoffice.Api`
* `src/NatureProtector.Postgres.Bootstrap`
* `src/NatureProtector.AppHost`, experimental, se presente

### 5.2 Pontos de entrada atuais e hotspots

* composição atual do simulador:

  * `src/NatureProtector.Simulator.Host/Program.cs`
* composição atual da prevenção:

  * `src/NatureProtector.Prevention.Host/Program.cs`
* pipeline de processamento:

  * `src/NatureProtector.Prevention.Host/Processing/ReadingEventProcessingService.cs`
  * `src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs`
  * `src/NatureProtector.Prevention.Host/Processing/PostgresReadingEventInbox.cs`
  * `src/NatureProtector.Prevention.Host/Processing/InboxRetryWorker.cs`
* persistência operacional:

  * `src/NatureProtector.Prevention.Host/Persistence/PostgresAcceptedReadingRepository.cs`
  * `src/NatureProtector.Prevention.Host/Persistence/PostgresRiskAssessmentRepository.cs`
  * `src/NatureProtector.Prevention.Host/Persistence/PostgresAreaRiskSnapshotRepository.cs`
  * `src/NatureProtector.Prevention.Host/Projection/PostgresAreaOperationalProjectionStore.cs`
* plano de controlo:

  * `src/NatureProtector.Infrastructure.Postgres/Bootstrap/ControlPlaneBootstrapper.cs`
  * `src/NatureProtector.Postgres.Bootstrap/Program.cs`
* contratos e topologia:

  * `src/NatureProtector.Shared/Contracts/*`
  * `src/NatureProtector.Shared/Messaging/*`
  * `src/NatureProtector.Shared/Configuration/RabbitMqOptions.cs`
* API:

  * `src/NatureProtector.Backoffice.Api/Program.cs`
  * `src/NatureProtector.Backoffice.Api/ControlPlane/Services/PostgresControlPlaneService.cs`
* observabilidade:

  * `src/NatureProtector.Shared/Observability/*`
  * `src/NatureProtector.Infrastructure.Influx/Services/InfluxWriteService.cs`

### 5.3 Lacunas atuais face à fase alvo

* já existe integração runtime com `PostgreSQL` para `control`, pipeline durável e projeções, com mitigação de duplicados concorrentes nos adaptadores principais; ainda falta validar esta política em cenários end-to-end mais longos;
* já existe baseline preparada em ficheiros e documentação de datasets/manifests, mas ainda falta fechar a rastreabilidade completa entre datasets, cenários, runs e resultados;
* já existe inbox durável, rejeição técnica, validação semântica, normalização interna e `RiskInput`; ainda falta fechar a semântica completa `accepted / rejected / normalized` como eventos ou artefactos arquiteturais estáveis;
* a API já expõe a superfície principal de leitura, mas ainda não fecha comandos e estados operacionais mais ricos;
* o simulador é útil e determinístico, mas ainda não está formalmente separado em verdade física, observação e falha de transporte;
* a prevenção ainda contém lógica de runtime no host que futuramente poderá migrar para módulo próprio de pipeline;
* os testes ficaram significativamente melhores na persistência PostgreSQL, mas ainda há lacunas em classificadores, API controllers, InfluxDB e casos canónicos.

## 6. Próximo Marco Técnico: Baseline V1 Estabilizada, Documentada e Testável

### 6.1 Objetivo

Fechar uma baseline de prevenção que possa ser compilada, executada, testada, demonstrada e explicada sem depender de interpretações implícitas do repositório.

Este marco não tem como objetivo implementar toda a V1 final. O objetivo é estabilizar a base sobre a qual a V1 final será construída.

### 6.2 Entregáveis mínimos

* README principal atualizado;
* roadmap atualizado;
* revisão de código e desenho documentada nos documentos arquiteturais existentes;
* build limpo e repetível;
* testes verdes;
* coverage consolidada atualizada;
* cobertura elevada nos componentes PostgreSQL da `NatureProtector.Prevention.Host`;
* decisão documentada sobre o estado experimental do `AppHost`;
* registo claro de `InfluxDB` como potencial gargalo local;
* lista explícita de tarefas bloqueadas por pesquisa;
* documentação clara sobre o que está implementado, parcial, experimental e pendente.

### 6.3 Critérios de saída

* `dotnet build` passa;
* `dotnet test` passa;
* coverage consolidada é gerada sem erro;
* `NatureProtector.Prevention.Host` mantém cobertura materialmente superior à baseline anterior;
* os componentes PostgreSQL críticos da pipeline têm testes comportamentais;
* README e roadmap refletem o estado real da implementação;
* não existem outputs gerados acidentalmente versionados;
* o estado experimental do `AppHost` fica documentado e fora do caminho crítico da solução;
* os próximos riscos técnicos estão documentados e ordenados.

### 6.4 Riscos prioritários após este marco

1. custo síncrono das escritas para `InfluxDB` quando a observabilidade temporal está ativa;
2. ausência de recuperação automática para eventos que fiquem em `Processing` após interrupção do host;
3. validação end-to-end da idempotência concorrente em execuções mais longas;
4. simulador ainda não separado em camadas metodológicas;
5. semântica `accepted/rejected/normalized` ainda incompleta como eventos ou artefactos estáveis;
6. score e alertas finais bloqueados por maturação da pesquisa.

## 7. Objetivos de Entrega da Fase Atual

O projeto só deve ser considerado concluído para esta fase quando todas as condições seguintes forem verdadeiras:

1. a estrutura do repositório reflete claramente a arquitetura pretendida ou, quando ainda não a reflete, documenta a diferença;
2. a configuração do plano de controlo é durável, versionada e gerida através de `PostgreSQL`;
3. os datasets estão catalogados, versionados e associados a cenários de forma reprodutível;
4. o simulador produz outputs plausíveis, rastreáveis, configuráveis e determinísticos;
5. a pipeline de execução é durável, auditável e tolerante à entrega duplicada;
6. o risco é calculado a partir de dados aceites, classificados e normalizados, não diretamente a partir de mensagens raw;
7. alertas e projeções são etapas explícitas, não efeitos secundários implícitos;
8. o sistema expõe observabilidade suficiente para demonstrar o fluxo end-to-end;
9. existem testes suficientes para validar os caminhos críticos;
10. a documentação principal permite a um avaliador compreender o estado real do projeto.

## 8. Regra de Ordenação do Trabalho

O projeto não deve continuar a avançar como se bastasse acrescentar funcionalidades em cima da forma atual do repositório.

O repositório já adiantou parte das fases de PostgreSQL, datasets, cenários, pipeline e projeções antes de fechar totalmente a modularização arquitetural. Esse desvio foi útil, porque reduziu incerteza sobre artefactos, regras, cenários e necessidades do simulador.

A partir deste ponto, a ordem recomendada é:

1. estabilizar a baseline técnica existente;
2. fechar build, testes, coverage e documentação principal;
3. corrigir riscos curtos de runtime, especialmente concorrência e validação cruzada;
4. reduzir ou parametrizar o custo local de `InfluxDB`;
5. consolidar a semântica `accepted / rejected / normalized`;
6. preparar `RiskInput`, `NormalizedReading` e casos canónicos;
7. só depois retomar modularização estrutural maior;
8. implementar score, alertas e agregação final quando a pesquisa estiver suficientemente consolidada.

Esta ordem minimiza refatorações destrutivas e evita implementar comportamento final antes de a investigação estar suficientemente fechada.

## 9. Estrutura Alvo Futura do Repositório

A estrutura alvo continua válida como direção estratégica, mas não deve ser executada como refatoração imediata sem necessidade clara.

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
  NatureProtector.Prevention.Host.Tests/
  NatureProtector.Simulator.Host.Tests/
  NatureProtector.Backoffice.Api.Tests/
  NatureProtector.Infrastructure.Postgres.Tests/
  NatureProtector.Infrastructure.Influx.Tests/
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

### 9.1 Responsabilidades alvo dos projetos

#### `NatureProtector.Core`

* conceitos de área, grelha, sensor, cenário, regra, risco e alerta que pertencem ao domínio;
* nenhum comportamento específico de broker, armazenamento, HTTP ou runtime.

#### `NatureProtector.Contracts`

* envelope de evento;
* tipos de evento;
* contratos de payload;
* DTOs públicos partilhados entre hosts e API.

#### `NatureProtector.Simulation`

* modelo de verdade física;
* modelo de erro do sensor;
* modelo de falha de transporte;
* executor de cenários;
* modelo de configuração da simulação.

#### `NatureProtector.Pipeline`

* ingestão;
* validação;
* normalização;
* idempotência;
* máquina de estados da inbox;
* quality flags;
* classificação de retries;
* produção de `ReadingAccepted`, `ReadingRejected` e `ReadingNormalized`.

#### `NatureProtector.Prevention`

* modelo de risco;
* `RiskInput`;
* agregação;
* política de alertas;
* geração de recomendações;
* construção de projeções.

#### `NatureProtector.Infrastructure.Postgres`

* EF Core;
* migrations;
* persistência do plano de controlo;
* persistência do catálogo de datasets;
* persistência da inbox;
* persistência de tentativas, rejeições e quarentena;
* persistência do estado de alertas e projeções.

#### `NatureProtector.Infrastructure.RabbitMq`

* opções de `RabbitMQ`;
* topologia;
* routing keys;
* publishers;
* consumers;
* abstrações de transporte, se necessárias.

#### `NatureProtector.Infrastructure.Influx`

* escrita de telemetria aceite;
* escrita de telemetria normalizada ou derivada;
* séries temporais de risco;
* séries temporais de alertas;
* observabilidade temporal.

#### `NatureProtector.Backoffice.Api`

* endpoints de configuração;
* endpoints de cenários;
* endpoints de datasets;
* endpoints de simulation runs;
* endpoints de projeções;
* endpoints de estado operacional;
* endpoints de alertas.

#### `NatureProtector.Simulator.Host`

* arranque do host;
* orquestração;
* composição de dependências;
* publicação de eventos.

#### `NatureProtector.Prevention.Host`

* arranque do host;
* composição de dependências;
* workers;
* ligação entre transporte, pipeline e prevenção.

## 10. Projetos a Criar, Mover e Remover Futuramente

Esta secção descreve a direção futura. Não deve ser executada automaticamente no próximo marco.

### 10.1 Criar futuramente

* `src/NatureProtector.Contracts`
* `src/NatureProtector.Simulation`
* `src/NatureProtector.Pipeline`
* `src/NatureProtector.Infrastructure.RabbitMq`
* `tests/NatureProtector.Contracts.Tests`
* `tests/NatureProtector.Simulation.Tests`
* `tests/NatureProtector.Pipeline.Tests`

Nota: `NatureProtector.Infrastructure.Postgres` já existe e deve ser estabilizado antes de qualquer extração maior.

### 10.2 Mover a partir dos projetos atuais

#### De `NatureProtector.Shared`

Mover futuramente para `NatureProtector.Contracts`:

* `Contracts/Readings/*`
* `Messaging/EventEnvelope.cs`
* `Messaging/EventTypes.cs`
* `Messaging/JsonEventSerializer.cs`

Mover futuramente para `NatureProtector.Infrastructure.RabbitMq`:

* `Configuration/RabbitMqOptions.cs`
* `Messaging/NatureProtectorRabbitMqTopology.cs`
* `Messaging/RoutingKeys.cs`

Manter ou avaliar:

* `Observability/*`, dependendo da estratégia de observabilidade transversal.

#### De `NatureProtector.Simulator.Host`

Mover futuramente para `NatureProtector.Simulation`:

* `Context/ScenarioContextFactory.cs`
* `Context/SimulationContext.cs`
* `Services/SeedProvider.cs`
* `Services/ReadingGenerationService.cs`
* `Configuration/SimulatorOptions.cs`

Manter em `NatureProtector.Simulator.Host`:

* `Program.cs`;
* peças de orquestração exclusivas do host;
* ligação ao publisher;
* composição de dependências.

#### De `NatureProtector.Prevention.Host`

Mover futuramente para `NatureProtector.Pipeline`:

* validação;
* normalização;
* classificadores;
* inbox;
* retry;
* quarentena;
* políticas de processamento.

Manter em `NatureProtector.Prevention.Host`:

* bootstrap do host;
* workers;
* composição de dependências;
* ligação ao RabbitMQ.

#### De `NatureProtector.Prevention`

Manter e expandir:

* scoring de risco;
* agregação de risco;
* política de alertas;
* recomendações;
* projeções;
* explicabilidade.

### 10.3 Remover após migração

* `src/NatureProtector.Shared` como projeto agregador genérico de longo prazo;
* resíduos de ingestão do lado do simulador, se ainda existirem;
* pastas ou namespaces com nomes incorretos ou obsoletos;
* artefactos gerados indevidamente versionados;
* duplicações entre documentação antiga e documentação atual.

## 11. Estratégia de Armazenamento de Datasets

Os datasets devem ser divididos em três camadas.

### 11.1 Camada de ficheiros

Os artefactos reais dos datasets vivem no sistema de ficheiros, não diretamente em `PostgreSQL` nem em `InfluxDB`.

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

* `baseline/` armazena artefactos pequenos e preparados que definem o input canónico da demonstração;
* `external/` armazena inputs raw descarregados ou importados e deve, normalmente, estar em `.gitignore`;
* `runtime/` armazena outputs gerados e deve estar em `.gitignore`;
* cada dataset preparado de área deve ter um `manifest.json` com origem, versão, checksum e data.

### 11.2 Camada de metadados em PostgreSQL

`PostgreSQL` armazena metadados e associações, não os datasets raw completos.

Deve responder a:

* que versão de dataset foi usada;
* que cenário foi associado a que artefactos de dataset;
* que configuração estava ativa;
* que fórmulas e limiares estavam ativos;
* que execution run usou essa combinação exata.

### 11.3 Camada operacional

`InfluxDB` armazena:

* leituras aceites;
* leituras normalizadas, se forem retidas como measurement;
* séries de risco;
* séries de alertas;
* métricas de observabilidade.

## 12. Estrutura PostgreSQL Alvo

A separação por schemas deve tornar explícita a diferença entre configuração, pipeline e projeções.

### 12.1 Schema `control`

Responsável por configuração, área piloto, grelha, sensores, cenários, artefactos e runs.

Tabelas ou equivalentes:

* `control.configuration_versions`;
* `control.areas`;
* `control.grid_cells`;
* `control.area_contexts`, se existir como entidade separada;
* `control.sensor_profiles`;
* `control.sensor_networks`;
* `control.sensor_nodes`;
* `control.scenario_definitions`;
* `control.rule_set_versions`, se aplicável;
* `control.dataset_artifacts`;
* `control.scenario_dataset_bindings`;
* `control.simulation_runs`.

### 12.2 Schema `pipeline`

Responsável pelo processamento durável de eventos.

Tabelas ou equivalentes:

* `pipeline.event_inbox`;
* `pipeline.event_processing_attempts`;
* `pipeline.rejected_events`, se separado;
* `pipeline.quarantined_events`, se separado;
* estados e metadados de retry, se modelados em tabelas próprias.

### 12.3 Schema `projection`

Responsável por resultados consultáveis e estado operacional.

Tabelas ou equivalentes:

* leituras aceites persistidas, se o modelo atual as colocar nesta camada;
* avaliações de risco;
* snapshots de risco por área;
* projeções operacionais por área;
* projeções operacionais por célula;
* alertas ativos;
* histórico mínimo de alertas, se necessário.

### 12.4 O que não deve ser tabela PostgreSQL de primeira onda

Não deve ser prioridade construir um warehouse relacional completo de cada leitura e métrica.

Não na primeira onda:

* espelho relacional completo da telemetria raw do `InfluxDB`;
* RBAC completo, exceto se a API precisar disso para a entrega;
* event store histórico completo de todas as projeções;
* tabelas finais de score se a pesquisa ainda não fechou completamente o modelo.

## 13. Catálogo Inicial de Eventos

### 13.1 Envelope

Todos os eventos devem usar o envelope comum:

* `schema_version`;
* `event_id`;
* `correlation_id`;
* `producer`;
* `event_type`;
* `area_id`;
* `event_time`;
* `ingest_time`;
* `payload`.

### 13.2 Conjunto inicial de eventos

| Evento                      | Producer                       | Consumers principais                 | Estado         |
| --------------------------- | ------------------------------ | ------------------------------------ | -------------- |
| `ConfigChanged`             | Backoffice / plano de controlo | hosts, auditoria, observabilidade    | futuro         |
| `ScenarioActivated`         | gestão de cenários             | simulador, API, observabilidade      | futuro/parcial |
| `ScenarioStopped`           | gestão de cenários             | simulador, API, observabilidade      | futuro         |
| `SensorNetworkInstantiated` | plano de controlo              | simulador, observabilidade           | futuro/parcial |
| `SensorReadingProduced`     | simulador                      | pipeline, observabilidade            | ativo          |
| `SensorBatchProduced`       | simulador                      | pipeline                             | opcional       |
| `SensorFaultRaised`         | simulador                      | pipeline, observabilidade, API       | futuro         |
| `ReadingAccepted`           | pipeline                       | InfluxDB, prevenção, observabilidade | alvo           |
| `ReadingRejected`           | pipeline                       | auditoria, observabilidade           | parcial/alvo   |
| `ReadingNormalized`         | pipeline                       | prevenção, persistência              | alvo           |
| `RiskEvaluated`             | prevenção                      | projeções, alertas, API              | parcial        |
| `AreaRiskAggregated`        | prevenção                      | projeções, alertas, API              | parcial        |
| `WarningRaised`             | alertas                        | persistência, API, observabilidade   | futuro         |
| `AlarmRaised`               | alertas                        | persistência, API, observabilidade   | futuro         |
| `RecommendationGenerated`   | alertas                        | persistência, API                    | futuro         |
| `ProjectionUpdated`         | projeções                      | API, UI                              | parcial        |

## 14. Roadmap por Fase

## Fase 0, Congelar a Baseline Técnica

### Objetivo

Transformar as decisões atuais em contratos de implementação, documentação executável e critérios de validação.

### Estado atual

Parcialmente concluída. Já existe documentação útil para navegação, implementação e operação, mas ainda há artefactos formais a consolidar.

### Tarefas

* manter `README.md` alinhado com o estado real;
* manter `docs/architecture/implementation.md` como ponto de entrada técnico;
* criar ou atualizar `docs/contracts/event-catalog.md`;
* criar ou atualizar `docs/architecture/module-baseline.md`;
* criar ou atualizar `docs/simulation/simulation-spec.md`;
* documentar estado do `AppHost`;
* documentar InfluxDB como potencial gargalo local;
* manter a revisão técnica alinhada nos documentos arquiteturais existentes.

### Critérios de saída

* não existe ambiguidade relevante sobre onde vivem configuração, datasets, runtime, telemetria e projeções;
* o repositório compila;
* os testes passam;
* a coverage consolidada está documentada;
* os principais riscos técnicos estão registados.

## Fase 1, Limpeza do Repositório e Extração Modular

### Objetivo

Fazer com que a estrutura do repositório reflita a arquitetura alvo, sem destabilizar a baseline atual.

### Estado atual

Em aberto. A estrutura alvo está identificada, mas não deve ser executada de imediato sem necessidade clara.

### Tarefas

* decidir quando criar:

  * `NatureProtector.Contracts`;
  * `NatureProtector.Simulation`;
  * `NatureProtector.Pipeline`;
  * `NatureProtector.Infrastructure.RabbitMq`;
* mover contratos e topologia RabbitMQ para fora de `NatureProtector.Shared`;
* mover serviços de simulação para fora de `NatureProtector.Simulator.Host`;
* mover lógica de pipeline para fora de `NatureProtector.Prevention.Host`;
* reduzir os hosts a projetos de composição;
* remover artefactos experimentais ou gerados indevidamente da solution.

### Critérios de saída

* os hosts ficam finos e focados em orquestração;
* contratos partilhados ficam separados de detalhes de infraestrutura;
* a modularização não quebra build, testes nem baseline de demonstração.

## Fase 2, Plano de Controlo em PostgreSQL

### Objetivo

Consolidar `PostgreSQL` como fonte de verdade para configuração, cenários, sensores, datasets e runs.

### Estado atual

Materialmente adiantada. O plano de controlo já existe em runtime, com bootstrap e integração com simulador.

### Tarefas restantes

* rever e documentar migrations existentes;
* consolidar metadados de datasets;
* garantir rastreabilidade completa entre cenário, configuração, dataset e `SimulationRun`;
* validar sincronização entre catálogo de cenários, bootstrap e sensores ativos;
* expor lacunas relevantes pela API, quando necessário.

### Critérios de saída

* simulador e prevenção conseguem resolver a configuração ativa a partir de `PostgreSQL`;
* cada run pode ser associada a configuração, cenário e artefactos de dataset;
* alterações no plano de controlo não deixam sensores obsoletos ativos indevidamente.

## Fase 3, Catálogo de Datasets e Inputs de Baseline

### Objetivo

Definir que datasets existem, onde vivem, como são versionados e como as runs se referem a eles.

### Estado atual

Materialmente adiantada. A baseline da área piloto, os manifests e os cenários executáveis já existem em ficheiros; falta consolidar a rastreabilidade completa com PostgreSQL e runs.

### Tarefas

* consolidar `data/baseline/areas/proenca-a-nova/`;
* consolidar manifests por área;
* carregar metadados de artefactos para `PostgreSQL`;
* criar ou validar associações cenário-dataset;
* fixar convenções de ficheiros para:

  * limite da área;
  * grelha;
  * atributos das células;
  * referência meteorológica;
  * histórico de incêndios;
  * candidatos a cenário;
  * cenários executáveis.

### Critérios de saída

* uma simulation run pode ser rastreada até aos artefactos exatos de dataset;
* a baseline pode ser reconstruída de forma repetível.

## Fase 4, Motor de Simulação

### Objetivo

Construir o simulador na forma em camadas exigida pela investigação.

### Estado atual

Parcialmente adiantada. Já existe execução determinística, contexto vindo de PostgreSQL e cenários de base, mas falta a separação explícita entre verdade física, erro de sensor e falha de transporte.

### Tarefas

* definir `TruthSnapshot`;
* definir `LocalObservation`;
* definir `OperationalEvent`;
* definir `IPhysicalScenarioModel`;
* definir `ISensorErrorModel`;
* definir `ITransportFaultModel`;
* implementar modelo físico de baseline;
* implementar perfis de sensores;
* implementar degradação controlada do Cenário C;
* validar que mesma seed e mesmo cenário produzem sequência equivalente;
* garantir que Cenário C preserva a base física do cenário limpo.

### Critérios de saída

* o simulador produz outputs determinísticos;
* a degradação de observação/transporte é separável da verdade física;
* os cenários A, B e C são explicáveis e testáveis.

## Fase 5, Pipeline Durável e Normalização

### Objetivo

Consolidar um fluxo durável, idempotente, auditável e semanticamente explícito.

### Estado atual

Materialmente adiantada em durabilidade, retries, quarentena, rejeição técnica, validação semântica, normalização interna e persistência. Parcial na semântica `accepted/rejected/normalized` como superfície arquitetural estável.

### Tarefas

* manter a mitigação de idempotência concorrente já aplicada nos adaptadores principais e validá-la em cenários end-to-end;
* manter a validação cruzada entre `AreaId` do envelope e deployment do sensor;
* consolidar estados:

  * `Accepted`;
  * `Rejected`;
  * `InvalidButStorable`;
  * `Normalized`;
  * `RetryPending`;
  * `Quarantined`;
  * `ExcludedFromRisk`;
* evoluir `NormalizedReading` e `RiskInput` de fronteiras internas implementadas para contratos semânticos plenamente documentados;
* emitir ou registar explicitamente:

  * `ReadingAccepted`;
  * `ReadingRejected`;
  * `ReadingNormalized`;
* armazenar tentativas de processamento;
* deduplicar por `event_id`;
* basear ordering e reconstrução em `event_time` sempre que aplicável.

### Critérios de saída

* duplicação de mensagens não cria efeitos de negócio duplicados;
* aceitação, rejeição e normalização são estados explícitos;
* eventos inválidos não contaminam leituras aceites;
* eventos de sensor/área incoerentes são rejeitados ou quarentenados de forma formal;
* a pipeline é auditável por PostgreSQL e observabilidade.

## Fase 6, Prevenção, Alertas e Projeções

### Objetivo

Calcular outputs operacionais apenas a partir de dados elegíveis, classificados e normalizados.

### Estado atual

Parcial. Já existe cálculo preliminar de risco, snapshots e projeções, mas ainda não o score final da V1.

### Tarefas

* adaptar a prevenção para consumir `RiskInput`;
* implementar `DailyCellState`, quando a pesquisa fechar parâmetros suficientes;
* implementar FWI/KBDI final, quando metodologicamente fechado;
* calcular risco por célula;
* agregar risco por área;
* preservar explicação e fatores dominantes;
* implementar política de warning;
* implementar política de alarm;
* implementar histerese;
* implementar cooldown;
* implementar recomendações;
* construir projeções operacionais consultáveis.

### Critérios de saída

* risco, warning e alarm são estados distintos;
* outputs têm justificação técnica;
* API/UI consomem projeções, não estado interno bruto;
* cálculo final não depende de mensagens raw.

## Fase 7, API e Superfície de Consulta

### Objetivo

Expor uma interface estável de controlo e consulta.

### Estado atual

Parcialmente adiantada. A API já expõe leitura sobre configuração, áreas, sensores, cenários, simulation runs e estado operacional básico.

### Tarefas

* consolidar endpoints de configuração;
* consolidar endpoints de áreas/grelha/sensores;
* consolidar endpoints de cenários;
* adicionar ou estabilizar endpoints do catálogo de datasets;
* adicionar endpoints de projeções;
* adicionar endpoints de histórico de runs;
* adicionar endpoints de alertas;
* tratar estados vazios/degradados de forma explícita;
* manter paginação defensiva.

### Critérios de saída

* API permite explorar a baseline sem depender diretamente da base de dados;
* UI ou demonstração conseguem consumir projeções e estado operacional;
* casos sem dados são tratados de forma previsível.

## Fase 8, Observabilidade, InfluxDB e Validação

### Objetivo

Tornar o sistema demonstrável, diagnosticável e testável.

### Estado atual

Parcialmente adiantada. Já existe observabilidade inicial, InfluxDB e Grafana, mas InfluxDB é candidato a gargalo local.

### Tarefas

* manter documentado e testado o modo local com `InfluxDb:Enabled=false`;
* avaliar batching ou desacoplamento das escritas para `InfluxDB`;
* manter measurements e tags documentados;
* adicionar dashboards para:

  * throughput;
  * backlog;
  * latência;
  * accepted/rejected/normalized;
  * supressão de duplicados;
  * retry/quarantine;
  * risco por área;
  * alertas ativos;
* documentar queries úteis;
* documentar proveniência e outputs de validação.

### Critérios de saída

* o projeto pode ser demonstrado end-to-end com evidência;
* a observabilidade não impede o diagnóstico local da pipeline;
* há separação clara entre estado durável e telemetria temporal.

## Fase 9, Testes e Casos Canónicos

### Objetivo

Garantir que a baseline evolui com segurança e que os comportamentos centrais são verificáveis.

### Estado atual

Significativamente melhorado. A coverage global e a coverage da `Prevention.Host` subiram de forma material, especialmente nos adaptadores PostgreSQL.

### Tarefas

* manter `dotnet build`;
* manter `dotnet test`;
* manter coverage consolidada;
* reforçar testes de classificadores;
* reforçar testes da Backoffice API;
* reforçar testes de InfluxDB;
* criar casos canónicos:

  * cenário normal;
  * cenário severo;
  * cenário degradado;
  * duplicado;
  * evento inválido;
  * evento atrasado;
  * score parcial;
  * alerta/projeção;
* adicionar testes end-to-end quando a pipeline estabilizar semanticamente.

### Critérios de saída

* testes cobrem caminhos críticos, não apenas DTOs;
* casos canónicos podem ser usados em demonstração e regressão;
* coverage permanece elevada sem criar testes frágeis.

## 15. Backlog de Implementação Ordenado

## ARCH

* `ARCH-01` Manter build e solution limpos.
* `ARCH-02` Corrigir ou remover artefactos experimentais indevidos da solution.
* `ARCH-03` Separar futuramente `NatureProtector.Shared` em `Contracts` e `Infrastructure.RabbitMq`.
* `ARCH-04` Extrair futuramente serviços de simulação para `NatureProtector.Simulation`.
* `ARCH-05` Extrair futuramente lógica de pipeline para `NatureProtector.Pipeline`.
* `ARCH-06` Reduzir hosts a composição quando a modularização for segura.

## DATA

* `DATA-01` Consolidar `data/baseline`, `data/manifests`, `data/external`, `data/runtime`.
* `DATA-02` Formalizar schema de `manifest.json`.
* `DATA-03` Consolidar ficheiros baseline da área piloto.
* `DATA-04` Registar artefactos de dataset da área piloto em `PostgreSQL`.
* `DATA-05` Associar cenários a datasets.
* `DATA-06` Garantir rastreabilidade dataset → cenário → run → outputs.

## PG

* `PG-01` Rever migrations existentes.
* `PG-02` Consolidar `control.configuration_versions`.
* `PG-03` Consolidar `control.areas` e `control.grid_cells`.
* `PG-04` Consolidar `control.sensor_profiles`, `sensor_networks` e `sensor_nodes`.
* `PG-05` Consolidar `control.scenario_definitions`.
* `PG-06` Consolidar `control.dataset_artifacts`.
* `PG-07` Consolidar `control.scenario_dataset_bindings`.
* `PG-08` Consolidar `control.simulation_runs`.
* `PG-09` Consolidar `pipeline.event_inbox`.
* `PG-10` Consolidar `pipeline.event_processing_attempts`.
* `PG-11` Consolidar rejeições e quarentena.
* `PG-12` Consolidar projeções operacionais.
* `PG-13` Validar idempotência concorrente dos adaptadores principais em cenários end-to-end.

## SIM

* `SIM-01` Definir `TruthSnapshot`.
* `SIM-02` Definir `LocalObservation`.
* `SIM-03` Definir `OperationalEvent`.
* `SIM-04` Definir `IPhysicalScenarioModel`.
* `SIM-05` Definir `ISensorErrorModel`.
* `SIM-06` Definir `ITransportFaultModel`.
* `SIM-07` Implementar modelo físico de baseline.
* `SIM-08` Implementar perfis de erro dos sensores.
* `SIM-09` Implementar perfis de falha da pipeline.
* `SIM-10` Garantir determinismo por seed.
* `SIM-11` Implementar Cenário C como degradação controlada.
* `SIM-12` Publicar `SensorFaultRaised`, quando necessário.

## PIPE

* `PIPE-01` Validar envelope.
* `PIPE-02` Validar semântica.
* `PIPE-03` Manter validação `AreaId` do envelope contra deployment do sensor.
* `PIPE-04` Consolidar normalização explícita.
* `PIPE-05` Formalizar semanticamente `NormalizedReading`.
* `PIPE-06` Formalizar semanticamente `RiskInput`.
* `PIPE-07` Garantir idempotência por `event_id`.
* `PIPE-08` Consolidar persistência da inbox.
* `PIPE-09` Consolidar logging de tentativas.
* `PIPE-10` Emitir ou registar `ReadingAccepted`.
* `PIPE-11` Emitir ou registar `ReadingRejected`.
* `PIPE-12` Emitir ou registar `ReadingNormalized`.

## PREV

* `PREV-01` Refatorar prevenção para consumir `RiskInput`.
* `PREV-02` Implementar risco por célula.
* `PREV-03` Implementar agregação por área.
* `PREV-04` Implementar explicabilidade.
* `PREV-05` Implementar política de warning.
* `PREV-06` Implementar política de alarm.
* `PREV-07` Implementar histerese.
* `PREV-08` Implementar cooldown.
* `PREV-09` Implementar recomendações.
* `PREV-10` Construir projeções operacionais finais.

## API

* `API-01` Consolidar endpoints de configuração.
* `API-02` Consolidar endpoints de áreas, grelha e sensores.
* `API-03` Consolidar endpoints de cenários.
* `API-04` Adicionar endpoints do catálogo de datasets.
* `API-05` Consolidar endpoints de simulation runs.
* `API-06` Adicionar endpoints de projeções.
* `API-07` Adicionar endpoints de alertas.
* `API-08` Tratar estados vazios, parciais ou indisponíveis.

## OBS

* `OBS-01` Documentar custo atual das escritas em `InfluxDB`.
* `OBS-02` Manter modo local para reduzir/desligar `InfluxDB`.
* `OBS-03` Avaliar batching ou desacoplamento.
* `OBS-04` Rastrear supressão de duplicados.
* `OBS-05` Rastrear accepted/rejected/normalized.
* `OBS-06` Rastrear retry/quarantine.
* `OBS-07` Rastrear metadados de simulation run.
* `OBS-08` Consolidar dashboards reais em Grafana.

## TEST

* `TEST-00` Manter build, test e coverage consolidados.
* `TEST-01` Manter cobertura dos adaptadores PostgreSQL.
* `TEST-02` Reforçar classificadores e semântica da leitura.
* `TEST-03` Reforçar testes de API controllers.
* `TEST-04` Reforçar testes de InfluxDB.
* `TEST-05` Adicionar testes de simulação em camadas.
* `TEST-06` Adicionar testes de contratos.
* `TEST-07` Adicionar testes de casos canónicos.
* `TEST-08` Adicionar testes end-to-end progressivos.

## DOC

* `DOC-01` Manter README principal atualizado.
* `DOC-02` Manter `implementation.md` atualizado.
* `DOC-03` Manter roadmap sincronizado com código e pesquisa.
* `DOC-04` Manter a revisão de código e desenho alinhada nos documentos arquiteturais existentes.
* `DOC-05` Documentar decisões arquiteturais em ADRs quando estabilizadas.
* `DOC-06` Documentar limitações conhecidas da baseline local.

## 16. Backlog de Testes Detalhado

### TEST-00, Baseline de qualidade

* `dotnet build`;
* `dotnet test`;
* coverage consolidada;
* verificação de outputs gerados;
* verificação de `.gitignore`.

### TEST-01, Runtime PostgreSQL da pipeline

* `PostgresReadingEventInbox`;
* `PostgresAcceptedReadingRepository`;
* `PostgresRiskAssessmentRepository`;
* `PostgresAreaRiskSnapshotRepository`;
* `PostgresAreaOperationalProjectionStore`.

Casos:

* inserir evento novo;
* detetar duplicado;
* marcar processamento;
* marcar processado;
* marcar rejeitado;
* marcar retry;
* marcar quarentena;
* persistir leitura aceite;
* persistir avaliação de risco;
* persistir snapshot;
* criar/atualizar projeção.

### TEST-02, Classificação e semântica

* eventos inválidos;
* rejeição precoce;
* retry;
* quarentena;
* duplicados;
* `stale`;
* `lateness`;
* `out-of-order`;
* flags;
* exclusão do risco.

### TEST-03, Simulator

* determinismo;
* seed;
* sensores ativos;
* contexto vindo do plano de controlo;
* Cenário C preservando base física;
* separação futura entre física, observação e transporte.

### TEST-04, Backoffice API

* configurações;
* áreas;
* sensores;
* cenários;
* `simulation_runs`;
* estados operacionais;
* paginação;
* casos sem dados;
* serviço indisponível.

### TEST-05, Influx e observabilidade

* modo ativo;
* modo local/desligável;
* falha de escrita;
* measurements;
* tags;
* custo por evento;
* impacto na pipeline.

### TEST-06, Casos canónicos

* cenário normal;
* cenário severo;
* cenário degradado;
* duplicado;
* evento inválido;
* evento atrasado;
* score parcial;
* alerta;
* projeção.

## 17. Tarefas Bloqueadas por Pesquisa

As tarefas abaixo não devem ser tratadas como comportamento final da plataforma enquanto a pesquisa não estiver suficientemente consolidada:

* `FWI` final;
* `KBDI` final;
* score composto final;
* `DailyCellState` definitivo;
* alertas finais;
* agregação espacial final;
* validação metodológica final dos cenários;
* política final de recomendações.

Mesmo nestes casos, continuam autorizados nesta fase:

* interfaces;
* scaffolding técnico;
* modelos de dados;
* testes de contrato;
* documentação;
* casos canónicos;
* preparação de observabilidade;
* evidências de runtime;
* protótipos claramente assinalados como preliminares.

## 18. Decisões Práticas para a Fase Atual

* não fazer uma reorganização estrutural grande do repositório nesta iteração;
* não executar agora a modularização para `Contracts`, `Simulation`, `Pipeline` ou `Infrastructure.RabbitMq`;
* manter `PostgreSQL` como fonte de verdade para `control`, durabilidade da pipeline e projeções;
* tratar `InfluxDB` como componente útil e demonstrável, mas não como requisito para cristalizar a semântica final do modelo;
* tratar `src/NatureProtector.AppHost/` como camada experimental até existir integração estável na solution;
* distinguir sempre entre funcionalidade implementada, parcial, experimental e bloqueada por pesquisa;
* priorizar correções pequenas, cobertas por testes, antes de refatorações amplas;
* não implementar score/alertas finais sem a pesquisa suficientemente consolidada.

## 19. Primeiro Marco Recomendado Após Esta Revisão

O melhor primeiro marco já não é criar datasets do zero nem iniciar modularização estrutural ampla.

O primeiro marco recomendado é:

* repositório com build/test/coverage limpos;
* README principal atualizado;
* roadmap fundido e atualizado;
* revisão de código e desenho registada nos documentos arquiteturais existentes;
* coverage forte nos adaptadores PostgreSQL da pipeline;
* riscos técnicos priorizados;
* decisão explícita sobre AppHost;
* InfluxDB documentado como gargalo local provável;
* backlog imediato definido.

Depois deste marco, a equipa deve avançar para:

1. validar idempotência concorrente dos adaptadores PostgreSQL em cenários end-to-end;
2. manter a validação `AreaId` do envelope contra deployment do sensor coberta por testes;
3. medir novamente o modo InfluxDB ativo após o batch por evento;
4. consolidar semântica `accepted/rejected/normalized`;
5. preparar casos canónicos sobre `NormalizedReading`, `RiskInput` e elegibilidade;
6. só depois retomar modularização estrutural maior.

## 20. Definition of Done para a Fase Atual

A fase atual está concluída quando o projeto consegue demonstrar:

1. uma área piloto e uma grelha configuradas a partir de `PostgreSQL`;
2. um cenário associado a artefactos de dataset versionados;
3. uma simulation run determinística;
4. fluxo de eventos com idempotência;
5. distinção explícita entre aceitação, rejeição, normalização interna, retry e quarentena;
6. risco por célula e por área;
7. warnings e alarms com justificação;
8. projeções servidas à API/UI;
9. dashboards, logs e métricas que provam o comportamento end-to-end;
10. documentação principal alinhada com o código;
11. testes automatizados sobre os caminhos críticos;
12. coverage suficientemente alta nos módulos de runtime;
13. limitações conhecidas explicitamente documentadas;
14. tarefas dependentes de pesquisa identificadas e não tratadas como concluídas.
