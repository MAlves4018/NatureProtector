# NatureProtector V1 Overview

## 1. Estado executivo da V1

Este documento é a porta de entrada canónica para compreender o estado atual do NatureProtector V1. O seu objetivo é dar uma leitura coerente do projeto sem obrigar a começar por todos os documentos técnicos, todos os diagramas ou todos os testes. Não substitui a documentação especializada; organiza-a.

O NatureProtector V1 é, nesta fase, uma baseline técnica do subsistema de prevenção. A cadeia implementada permite preparar dados e cenários para a área piloto de `proenca-a-nova`, correr o simulador, publicar leituras por RabbitMQ, consumir essas leituras no host de prevenção, persistir estado operacional em PostgreSQL, escrever observabilidade temporal quando InfluxDB está ativo, expor projeções por API e recolher evidência runtime por run.

O estado atual deve ser lido com duas ideias em simultâneo:

- há uma runtime real, testável e demonstrável;
- a validação é técnica e operacional, não científica.

Isto significa que a V1 demonstra rastreabilidade, robustez de pipeline, separação de responsabilidades, scoring candidato e observabilidade. Não demonstra calibração oficial de risco de incêndio, equivalência com sistemas oficiais, validação multiárea ou previsão científica de incêndios rurais.

| Frente | Estado V1 | Leitura correta |
| --- | --- | --- |
| Runtime local | Implementado | `Simulator.Host`, RabbitMQ, `Prevention.Host`, PostgreSQL, InfluxDB opcional, Grafana e `Backoffice.Api` compõem a baseline local. |
| Contrato RabbitMQ | Implementado | O transporte vivo continua a ser `EventEnvelope<SensorReadingProducedPayload>` com evento `SensorReadingProduced`. |
| Camada interna `OperationalEvent` | Implementado | É adaptação interna da prevenção; não é contrato externo RabbitMQ. |
| Pipeline V1 | Implementado/parcial | Há normalização, elegibilidade, scoring, projeções, retry e quarentena, mas nem todas as famílias de eventos operacionais estão publicadas externamente. |
| Risco | Implementado como baseline candidata | `RiskAssessment` distingue `BaseRisk`, `AdjustedScore` e compatibilidade `RiskScore`; a fórmula não é calibração científica. |
| Alertas | Implementado como política interna V1 | `V1AlertPolicy` usa `None`, `Warning`, `Alarm` e histerese sobre score ajustado. |
| API | Implementado/parcial | Expõe estado persistido/projetado; não recalcula risco. |
| Orquestração de runs | Implementado localmente | `run-spec.json` e `run-scenario.ps1` controlam execuções e evidência. |
| Coverage | Validado tecnicamente | Estado consolidado recente: `97.6%` line, `90.1%` branch, `97.1%` method. |
| Ciência/modelo oficial | Fora de âmbito da validação atual | FWI/KBDI/PIR/RCM são referências e evolução metodológica, não equivalência implementada e validada. |

Fontes principais para aprofundar:

- [architecture/implementation.md](architecture/implementation.md)
- [architecture/architecture.md](architecture/architecture.md)
- [planning/v1-implementation-map.md](planning/v1-implementation-map.md)
- [planning/diary/MiguelAlves/MiguelAlves.md](planning/diary/MiguelAlves/MiguelAlves.md)
- [setup/local-baseline-setup.md](setup/local-baseline-setup.md)
- [runtime-developer-control.md](runtime-developer-control.md)
- [tests/README.md](../tests/README.md)

## 2. O que a V1 valida e o que não valida

A V1 valida sobretudo engenharia de software, arquitetura, integração e execução reprodutível. A pergunta que a V1 responde não é "este score é cientificamente calibrado para prever incêndios?", mas sim "o sistema consegue transformar leituras simuladas e contexto operacional em estado rastreável, auditável e consultável, preservando fronteiras corretas entre eventos, normalização, elegibilidade, risco, alertas e projeções?".

| Tipo de afirmação | Estado | Evidência aceitável |
| --- | --- | --- |
| O sistema processa eventos simulados end-to-end | Validado tecnicamente | Testes, execução local, PostgreSQL, evidência por run. |
| RabbitMQ transporta `SensorReadingProduced` em envelope comum | Validado tecnicamente | Código em `NatureProtector.Shared` e testes de compatibilidade. |
| Leituras bloqueadas não geram novo assessment numérico | Validado tecnicamente | `ReadingRiskPipeline`, `SimpleRiskScoringService` e testes. |
| A API lê projeções e alertas persistidos | Validado tecnicamente | `PostgresControlPlaneService` e testes da API. |
| A V1 usa parâmetros candidatos | Implementado como baseline | Explicações e mensagens marcam `Candidate Parameter Set V1.0`. |
| O score representa risco oficial de incêndio | Não validado | Fora de âmbito atual. |
| FWI/KBDI estão implementados como cálculo final | Não implementado como modelo final | Podem existir artefactos e contexto preparatório, mas não validação final. |
| O sistema está calibrado para outras áreas | Não validado | A área piloto é `proenca-a-nova`. |

O documento deve, por isso, manter sempre a separação entre:

- facto implementado: existe código, teste ou evidência runtime;
- intenção documental: objetivo ou direção descrita em docs;
- validação técnica: build, testes, coverage, execução e evidência;
- validação científica futura: calibração, comparação oficial, validação externa e generalização.

## 3. Arquitetura runtime atual

A runtime atual é uma baseline local orientada a demonstração, análise e validação técnica. A leitura principal é:

```text
Simulator.Host
  -> RabbitMQ
  -> Prevention.Host
  -> PostgreSQL
  -> Backoffice.Api
  -> consumidor humano, evidência e dashboards
```

InfluxDB e Grafana existem como observabilidade temporal e visualização de apoio. PostgreSQL é a fonte durável de estado operacional e controlo nesta fase.

| Componente | Papel atual | Estado |
| --- | --- | --- |
| `NatureProtector.Simulator.Host` | Resolve cenário, sensores e parâmetros; gera leituras; publica eventos. | Implementado |
| RabbitMQ | Broker entre simulador e prevenção. | Implementado |
| `NatureProtector.Prevention.Host` | Consome eventos, aplica inbox/retry/quarentena, normaliza, avalia elegibilidade, calcula risco e atualiza projeções. | Implementado |
| PostgreSQL | Control plane, inbox, tentativas, quarentena, logs e projeções. | Implementado |
| InfluxDB | Observabilidade temporal, configurável e não crítica por defeito. | Implementado/opcional |
| Grafana | Apoio a dashboards e leitura temporal. | Parcial |
| `NatureProtector.Backoffice.Api` | Consulta `control.*`, `pipeline.*`, `projection.*`, alertas ativos e resumo agregado do Runtime Monitor. | Implementado/parcial |
| `scripts/scenarios/run-scenario.ps1` | Orquestração local de runs por especificação. | Implementado |

O desenho não deve ser apresentado como deployment produtivo distribuído. A baseline é local, controlável e adequada para demonstração, mas ainda não é uma plataforma operacional em produção.

Documentos de detalhe:

- [architecture/current-capabilities-and-how-to-run.md](architecture/current-capabilities-and-how-to-run.md)
- [setup/local-baseline-setup.md](setup/local-baseline-setup.md)
- [architecture/postgresql-architecture.md](architecture/postgresql-architecture.md)

## 4. Contratos externos vs camadas internas

A distinção mais importante da V1 é entre contrato externo de transporte e camadas internas de domínio/pipeline.

O contrato RabbitMQ vivo continua a ser:

```text
EventEnvelope<SensorReadingProducedPayload>
EventType = SensorReadingProduced
routing key = simulation.reading.produced
```

`OperationalEvent` existe, mas como adaptador interno da prevenção. Ele traduz o envelope de transporte para uma forma mais conveniente para a pipeline. Não é, no estado atual, substituto do payload RabbitMQ nem evento externo publicado.

| Conceito | Estado | Localização principal | Leitura correta |
| --- | --- | --- | --- |
| `EventEnvelope<TPayload>` | Implementado | `src/NatureProtector.Shared/Messaging/EventEnvelope.cs` | Envelope de transporte comum. |
| `SensorReadingProducedPayload` | Implementado | `src/NatureProtector.Shared/Contracts/Readings/SensorReadingProducedPayload.cs` | Payload real transportado pelo simulador. |
| `SensorReadingProduced` | Implementado | `src/NatureProtector.Shared/Messaging/EventTypes.cs` | Evento externo vivo da ingestão. |
| `OperationalEvent` | Implementado internamente | `src/NatureProtector.Prevention/Readings/OperationalEvent.cs` | Adaptador interno `EventEnvelope -> pipeline`. |
| `NormalizedReading` | Implementado | `src/NatureProtector.Prevention/Readings/NormalizedReading.cs` | Leitura interna normalizada, enriquecida com qualidade e classificadores. |
| `ReadingAccepted`, `ReadingRejected`, `ReadingNormalized` | Parcial/alvo | `NatureProtector.Shared` e docs de contratos | Podem existir como nomes/constantes ou intenção, mas não são a família externa completa publicada end-to-end. |
| `WarningRaised`, `AlarmRaised` | Futuro | `docs/contracts/event-catalog.md` | Eventos formais futuros, não contrato vivo. |

Os documentos em [contracts](contracts/README.md) foram alinhados com o estado V1 atual para distinguir contratos externos, camadas internas e conceitos futuros. Quando houver conflito futuro entre documentação, código, testes e evidência runtime recente, devem prevalecer código, testes e evidência até a documentação ser corrigida.

## 5. Pipeline V1

A pipeline V1 transforma uma leitura recebida em estado operacional persistido. O caminho nominal é:

```text
EventEnvelope<SensorReadingProducedPayload>
  -> OperationalEvent
  -> NormalizedReading
  -> RiskEligibilityResult
  -> RiskInput
  -> RiskAssessment
  -> AreaRiskSnapshot
  -> Operational projections
  -> API / evidence / observability
```

O fluxo completo envolve mais do que scoring. Antes de qualquer cálculo de risco, o sistema valida contrato, materializa a inbox, distingue falhas técnicas e semânticas, normaliza a leitura e decide elegibilidade.

| Etapa | Papel | Estado |
| --- | --- | --- |
| Receção RabbitMQ | Deserializa envelope e payload. | Implementado |
| Validação pré-inbox | Rejeita invalidez técnica antes de materializar a mensagem. | Implementado |
| Inbox durável | Garante idempotência, tentativas e rastreabilidade. | Implementado |
| Retry/quarentena | Classifica falhas transitórias/permanentes. | Implementado |
| `OperationalEvent` | Adapta envelope para evento interno. | Implementado |
| `NormalizedReading` | Normaliza campos e transporta flags/classificadores. | Implementado |
| Elegibilidade | Decide se a leitura pode gerar risco. | Implementado |
| Scoring | Calcula assessment apenas para input elegível. | Implementado |
| Projeções | Atualiza estado por célula, área e alertas. | Implementado/parcial |
| Eventos derivados externos | Publicação formal de `accepted/normalized/warning/alarm`. | Parcial/futuro |

O caso `Blocked` é especialmente importante. Uma leitura pode ser aceite para auditoria e ainda assim não ser elegível para scoring. Quando a elegibilidade é `Blocked`, a pipeline conclui o processamento sem criar um novo `RiskAssessment` numérico.

## 6. Qualidade, classificadores e elegibilidade

A V1 introduziu uma camada explícita para qualidade e classificação. Isto evita que a pipeline trate todas as leituras aceites como igualmente boas para scoring.

Os tipos principais são:

- `ClassifierResult`;
- `ClassifierStatus`;
- `ClassifierSeverity`;
- `RiskEligibilityResult`;
- `RiskInputStatus`;
- `RiskEligibilityReason`;
- `QualityFlags`.

`ClassifierResult` carrega nome do classificador, estado, severidade, flags, próxima ação e razão auditável. Os resultados podem acompanhar `OperationalEvent`, `NormalizedReading`, `RiskEligibilityResult` e `RiskInput`.

| Estado de elegibilidade | Significado | Efeito |
| --- | --- | --- |
| `CompleteEligible` | A leitura tem condições completas para scoring. | Pode gerar `RiskAssessment`. |
| `PartialButUsable` | A leitura tem degradação ou incompletude tolerável. | Pode gerar `RiskAssessment` com fatores candidatos. |
| `Blocked` | A leitura não tem condições para novo score válido. | Não gera novo `RiskAssessment` numérico. |

O bloqueio não deve ser confundido com risco baixo. Risco baixo é um resultado numérico válido. `Blocked` é ausência de condições para calcular novo resultado válido.

Esta camada é validada por testes de domínio e pipeline, incluindo testes para `ClassifierResult`, `RiskEligibilityResult`, `RiskEligibilityService` e `RiskInput`.

## 7. Risco, RiskInput, DailyCellState e RiskAssessment

`RiskInput` é a fronteira entre pipeline e motor de scoring. Ele é pré-scoring. Deve conter informação necessária para calcular risco, mas não resultados de risco, alertas ou projeções.

No estado atual, `RiskInput` contém:

- área;
- sensor;
- evento de origem;
- métrica;
- valor;
- unidade;
- tempo do evento;
- estado de elegibilidade;
- razão de elegibilidade;
- confiança observacional;
- integridade operacional;
- flags de qualidade;
- classificadores.

Não deve conter:

- `BaseRisk`;
- `AdjustedScore`;
- `RiskScore`;
- `RiskLevel`;
- `AlertState`;
- projeção operacional.

`RiskAssessment` é o resultado do scoring. A V1 distingue:

| Campo | Papel |
| --- | --- |
| `BaseRisk` | Score base antes de fatores candidatos de contexto/confiança/integridade. |
| `AdjustedScore` | Score ajustado operacional usado para nível e projeções. |
| `RiskScore` | Campo de compatibilidade que espelha `AdjustedScore`. |
| `RiskLevel` | Nível qualitativo derivado do score ajustado. |
| `ExplanationSummary` | Explicação curta, incluindo parâmetros candidatos quando aplicável. |

O serviço atual de scoring é uma baseline candidata. Aplica thresholds simples por métrica e fatores candidatos para confiança observacional, integridade operacional e elegibilidade. A explicação inclui `Candidate Parameter Set V1.0 (non-calibrated)`, o que deve ser preservado na leitura metodológica: há modelo técnico executável, não calibração oficial.

`DailyCellState` existe como artefacto de contexto diário. A sua função é guardar contexto e memória diária por célula, incluindo informação útil para evolução metodológica futura. Ele não é score final e não deve ser apresentado como cálculo completo de FWI/KBDI. Pode apoiar essa evolução, mas a V1 atual não deve afirmar que implementa ou valida esses índices como produto científico.

## 8. Alertas, projeções e API

A política de alertas V1 é interna ao host de prevenção. Usa três estados:

- `None`;
- `Warning`;
- `Alarm`.

Os thresholds candidatos atuais são:

| Transição | Threshold |
| --- | --- |
| Abre `Warning` | `AdjustedScore >= 0.60` |
| Fecha `Warning` | `AdjustedScore < 0.50` |
| Abre `Alarm` | `AdjustedScore >= 0.80` |
| Mantém `Alarm` | `AdjustedScore >= 0.70` |
| Desce/fecha com histerese | abaixo dos thresholds de fecho |

O alerta é materializado nas projeções. A mensagem persistida inclui `AlertState=<estado>` e a API extrai esse valor. Isto é uma escolha pragmática da V1: a API expõe o estado persistido/projetado e não recalcula risco nem reexecuta a política de scoring.

| Superfície API | Papel | Observação |
| --- | --- | --- |
| Configurações | Consulta e ativação mínima de control plane. | API maioritariamente de leitura. |
| Áreas e grelha | Leitura do plano de controlo. | Depende do bootstrap PostgreSQL. |
| Cenários e runs | Consulta de cenários e simulation runs. | Útil para demonstração. |
| Estado operacional da área | Lê projeção persistida e `alertState`. | Não recalcula risco. |
| Estado operacional por célula | Lê projeção por célula. | Base para UI/backoffice. |
| Alertas ativos | Lista alertas abertos e `alertState`. | Lê `projection.alert_state`. |

Documentos e código de detalhe:

- [architecture/implementation.md](architecture/implementation.md)
- [src/NatureProtector.Backoffice.Api/README.md](../src/NatureProtector.Backoffice.Api/README.md)
- [src/NatureProtector.Prevention.Host/README.md](../src/NatureProtector.Prevention.Host/README.md)

## 9. Simulação, cenários e orquestração de runs

O simulador é parte da V1, não apenas ferramenta lateral. Ele permite gerar leituras reproduzíveis para cenários da área piloto, publicar eventos e alimentar a pipeline.

O estado atual suporta:

- resolução de cenário a partir do control plane em PostgreSQL;
- execução com seed;
- número de ciclos;
- intervalo entre ciclos;
- seleção de sensores;
- publicação RabbitMQ;
- `RunOverrides`;
- persistência de `simulation_runs`;
- metadata de overrides pedidos e resolvidos;
- orquestração local por `run-spec.json`.

O `run-spec.json` é o contrato operacional local da orquestração. O exemplo atual inclui:

| Campo | Papel |
| --- | --- |
| `areaCode` | Área alvo, como `proenca-a-nova`. |
| `scenarioCode` | Cenário alvo, como `scenario_b`. |
| `sensorCount` | Número de sensores pedido. |
| `numberOfCycles` | Número de ciclos da simulação. |
| `intervalSeconds` | Intervalo lógico entre ciclos. |
| `seed` | Seed de reprodutibilidade. |
| `degradationProfile` | Perfil operacional pedido. |
| `collectEvidence` | Controla recolha de evidência. |
| `waitForCompletion` | Espera pela conclusão da run. |
| `timeoutSeconds` | Timeout operacional. |
| `allowParallelRun` | Política de runs paralelas. |
| `runLabel` | Nome humano da execução. |

O caminho de execução local é:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\scenarios\run-scenario.ps1 `
  -SpecPath .\scripts\scenarios\examples\scenario-b-default.json
```

A documentação [architecture/scenario-run-orchestrator.md](architecture/scenario-run-orchestrator.md) descreve a evolução O1/O1.2: O1.1 criou o contrato local de execução e o script; O1.2 adicionou suporte real no `Simulator.Host` para `RunOverrides`, persistência de metadata requested/resolved e correlação por `orchestratorCorrelationId`.

## 10. Persistência e observabilidade

PostgreSQL é a persistência relacional central da V1. A organização lógica é:

| Schema | Papel |
| --- | --- |
| `control` | Configuração, áreas, grelha, sensores, cenários, datasets e simulation runs. |
| `pipeline` | Inbox, tentativas de processamento, rejeições e quarentena. |
| `projection` | Logs aceites, assessments, snapshots, estado operacional e alertas. |

InfluxDB é observabilidade temporal. A pipeline pode correr com `InfluxDb:Enabled=false`, usando writer no-op, mantendo PostgreSQL como estado durável. Quando InfluxDB está ativo, a escrita deve ser vista como apoio de análise temporal e dashboards, não como fonte de decisão de negócio.

Grafana existe como visualização de apoio. A maturidade dos dashboards ainda é parcial: útil para demonstrar observabilidade e explorar séries, mas não deve ser apresentado como cockpit operacional final.

Documentos especializados:

- [architecture/postgresql-architecture.md](architecture/postgresql-architecture.md)
- [architecture/pipeline-influx-options.md](architecture/pipeline-influx-options.md)
- [architecture/grafana-influx-dashboard-guide.md](architecture/grafana-influx-dashboard-guide.md)

## 11. Testes, coverage e evidência runtime

A V1 tem validação automática relevante. A cobertura consolidada recente é:

| Métrica | Valor |
| --- | --- |
| Line coverage | `97.6%` |
| Branch coverage | `90.1%` |
| Method coverage | `97.1%` |
| Full method coverage | `92.9%` |

Esta cobertura deve ser interpretada como validação técnica. Ela protege domínio, contratos, pipeline, API, Influx configurável, simulador, orquestração e casos críticos. Não transforma o modelo de risco em modelo cientificamente calibrado.

Famílias de testes relevantes:

| Família | Exemplos de comportamento protegido |
| --- | --- |
| `Core.Tests` | Áreas, sensores, cenários, risco e invariantes de domínio. |
| `Shared.Tests` | Envelope, serialização e contratos partilhados. |
| `Prevention.Tests` | `OperationalEvent`, `NormalizedReading`, `ClassifierResult`, elegibilidade, `RiskInput`, `DailyCellState`, scoring. |
| `Prevention.Host.Tests` | Worker, inbox, retry, quarentena, projection stores e `V1AlertPolicy`. |
| `Simulator.Host.Tests` | Contexto, runner, `RunOverrides`, seleção determinística de sensores. |
| `Backoffice.Api.Tests` | Endpoints, estado operacional e `alertState`. |
| `Infrastructure.Influx.Tests` | Configuração e fronteira de escrita temporal. |
| `IntegrationTests` | Compatibilidade curta entre simulador e prevenção. |

Além dos testes, existe evidência runtime por run em [evidence/runs](evidence/runs). A run mais recente usada como referência documental inclui `summary.md`, `run-spec.resolved.json`, logs do simulador e relatório runtime. Estes artefactos são importantes porque mostram execução observável e não apenas asserções unitárias.

## 12. Como correr localmente

O caminho manual de baseline é:

```powershell
.\infra\scripts\up.ps1
.\infra\scripts\smoke-test.ps1
.\scripts\postgres\bootstrap-control-plane.ps1
```

Depois, em terminais separados:

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Backoffice.Api
```

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Prevention.Host
```

```powershell
.\scripts\dotnet\Use-RepoDotnetEnvironment.ps1
dotnet run --project .\src\NatureProtector.Simulator.Host
```

Para runs reprodutíveis, preferir:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\scenarios\run-scenario.ps1 `
  -SpecPath .\scripts\scenarios\examples\scenario-b-default.json
```

Verificações úteis:

| Objetivo | Comando ou ponto de entrada |
| --- | --- |
| Ver infra local | `.\infra\scripts\smoke-test.ps1` |
| Ver control plane | `GET http://localhost:5254/api/control/configurations/active` |
| Ver áreas | `GET http://localhost:5254/api/control/areas` |
| Ver estado operacional | `GET http://localhost:5254/api/control/areas/proenca-a-nova/operational-state` |
| Ver runs | `GET http://localhost:5254/api/control/simulation-runs?areaCode=proenca-a-nova` |
| Ver RabbitMQ | `http://localhost:15672` |
| Ver Grafana | `http://localhost:3000` |

O guia detalhado está em [setup/local-baseline-setup.md](setup/local-baseline-setup.md).

## 13. Diagramas canónicos e estado dos diagramas

Os diagramas continuam úteis, mas nem todos têm o mesmo grau de atualização face a C0-C7 e O1/O1.2.
Alguns devem ser lidos apenas como contexto geral, outros estão parciais, e os
diagramas de pipeline/domínio precisam de atualização antes de serem usados como
fotografia rigorosa da V1 atual.

| Diagrama | Estado recomendado | Observação |
| --- | --- | --- |
| `01-platform-context` | Usar | Contexto geral estável. |
| `02-prevention-subsystem-landscape` | Usar com cautela | Bom para paisagem, rever detalhe V1 recente. |
| `03-end-to-end-data-chain` | Usar | Cadeia de dados continua útil. |
| `04-data-curation-and-provenance` | Usar | Apoia rastreabilidade de dados. |
| `07-simulator-layered-architecture` | Parcial | Atualizar com `RunOverrides` e orquestração. |
| `08-simulation-sequence-happy-path` | Parcial | Atualizar com `run-spec` e evidência por run. |
| `09-operational-pipeline-overview` | Atualização prioritária | Deve refletir `OperationalEvent`, classificadores, elegibilidade e `Blocked`. |
| `10-pipeline-retry-and-quarantine-sequence` | Parcialmente atual | Rever com classificadores e estados V1. |
| `11-persistence-views` | Parcial | Deve refletir `BaseRisk`, `AdjustedScore` e alert state V1. |
| `12-runtime-deployment-local-baseline` | Usar com cautela | Bom para baseline; pode incluir orquestrador. |
| `14-domain-model-simplified` | Desatualizado | Precisa incorporar V1 de risco/elegibilidade. |
| `15-domain-model-detailed` | Desatualizado | Falta representar corretamente campos V1 e `DailyCellState`. |
| `implementation-*` | Parcial | Bons para onboarding, mas alguns precisam atualização factual. |
| `00-legacy-current-repo-architecture` | Histórico | Não usar como estado atual. |

Prioridade recomendada para atualização posterior:

1. `09-operational-pipeline-overview`
2. `15-domain-model-detailed`
3. `14-domain-model-simplified`
4. `11-persistence-views`
5. `07-simulator-layered-architecture`
6. `08-simulation-sequence-happy-path`
7. `implementation-prevention-nominal-flow`
8. `implementation-api-read-paths`
9. `implementation-tests-map`
10. `12-runtime-deployment-local-baseline`

As imagens em [architecture/images](architecture/images) devem ser reexportadas depois de atualizar os `.drawio`.

## 14. Limitações conhecidas

As limitações abaixo são intencionais e devem ser comunicadas sem ambiguidade.

| Tema | Limitação |
| --- | --- |
| Validação científica | Não há calibração oficial nem validação externa do modelo de risco. |
| FWI/KBDI | Podem orientar contexto e evolução, mas não são cálculo final validado nesta V1. |
| Área piloto | A validação está centrada em `proenca-a-nova`; não há generalização multiárea validada. |
| Eventos derivados | A família externa de eventos `accepted/rejected/normalized/warning/alarm` não está totalmente publicada end-to-end. |
| Dashboards | Grafana é apoio de observabilidade, não cockpit final. |
| Backoffice | A API é maioritariamente de leitura; a superfície de gestão ainda é limitada. |
| Deploy produtivo | A baseline é local e demonstrável, não deployment de produção. |
| Orquestração | O orquestrador é local por PowerShell; integração via API/site é futura. |
| Fontes externas | Algumas fontes oficiais ou complementares permanecem dependentes de autenticação, disponibilidade ou curadoria adicional. |

## 15. Roadmap técnico

O roadmap técnico deve partir do que já está implementado, sem reabrir decisões fechadas. A ordem recomendada é:

1. consolidar este overview como documento canónico;
2. manter `docs/contracts` alinhado com contratos externos, camadas internas e conceitos futuros;
3. manter `scenario-run-orchestrator.md` alinhado com a evolução O1/O1.2 e futura integração no Backoffice/API;
4. refazer os diagramas de pipeline e domínio;
5. alinhar `architecture.md` e `implementation.md` com os pontos V1 recentes;
6. amadurecer dashboards operacionais;
7. evoluir a orquestração de PowerShell para serviço/API reutilizável;
8. separar melhor o simulador em verdade física, observação local e falha de transporte;
9. tratar FWI/KBDI e validação científica como frentes futuras separadas;
10. preparar validação multiárea apenas quando existirem dados, parâmetros e critérios adequados.

Documentos históricos como [planning/project-completion-roadmap.md](planning/project-completion-roadmap.md) e [planning/pipeline-gap-and-dependency-map.md](planning/pipeline-gap-and-dependency-map.md) devem ser usados como contexto histórico, não como fotografia atual quando contradizem código, testes ou evidência recente.

## 16. Mapa de documentos

| Documento | Quando ler | Estado para este overview |
| --- | --- | --- |
| [architecture/implementation.md](architecture/implementation.md) | Onboarding técnico detalhado por vistas. | Fonte direta, com sincronização pontual necessária. |
| [architecture/architecture.md](architecture/architecture.md) | Narrativa arquitetural ampla. | Fonte parcial; atualizar risco, alertas e orquestração. |
| [architecture/current-capabilities-and-how-to-run.md](architecture/current-capabilities-and-how-to-run.md) | Como correr manualmente a baseline. | Fonte parcial; complementar com `run-spec`. |
| [architecture/scenario-run-orchestrator.md](architecture/scenario-run-orchestrator.md) | Descrição operacional do orquestrador local O1/O1.2. | Fonte especializada atual, a manter quando a orquestração evoluir para API/site. |
| [architecture/postgresql-architecture.md](architecture/postgresql-architecture.md) | Papel de PostgreSQL. | Fonte especializada. |
| [architecture/pipeline-influx-options.md](architecture/pipeline-influx-options.md) | Política de Influx e escrita temporal. | Fonte especializada. |
| [contracts/event-catalog.md](contracts/event-catalog.md) | Catálogo de eventos externos, camadas internas e eventos futuros. | Fonte normativa curta, alinhada com o estado V1 atual. |
| [contracts/v1-vocabulary-map.md](contracts/v1-vocabulary-map.md) | Vocabulário V1 e compatibilidade. | Fonte normativa curta, alinhada com C0-C7 e O1/O1.2. |
| [planning/v1-implementation-map.md](planning/v1-implementation-map.md) | Mapa consolidado da frente V1. | Fonte direta principal. |
| [planning/diary/MiguelAlves/MiguelAlves.md](planning/diary/MiguelAlves/MiguelAlves.md) | Histórico recente e síntese da quinzena. | Fonte direta para C0-C7/O1/O1.2 e coverage. |
| [planning/project-completion-roadmap.md](planning/project-completion-roadmap.md) | Roadmap antigo. | Fonte histórica; não usar como verdade atual. |
| [planning/pipeline-gap-and-dependency-map.md](planning/pipeline-gap-and-dependency-map.md) | Lacunas iniciais. | Fonte histórica/parcial. |
| [setup/local-baseline-setup.md](setup/local-baseline-setup.md) | Setup local detalhado. | Fonte direta para execução manual. |
| [tests/README.md](../tests/README.md) | Estado da suite e coverage. | Fonte direta para validação técnica. |
| [evidence/runs](evidence/runs) | Evidência por execução. | Fonte direta para runtime observado. |

## Estado de manutenção deste documento

Este overview deve ser atualizado quando houver mudança relevante em qualquer uma destas frentes:

- contrato RabbitMQ ou payload externo;
- fronteira `OperationalEvent -> NormalizedReading -> RiskInput`;
- semântica de `RiskInputStatus`, especialmente `Blocked`;
- campos de `RiskAssessment`;
- política de alertas ou formato de `alertState`;
- endpoints de API operacional;
- orquestração por `run-spec`;
- coverage consolidado;
- evidência runtime de referência;
- maturidade de dashboards ou deployment;
- implementação real de FWI/KBDI ou validação científica.

Regra de manutenção: quando houver conflito entre este overview e o código/testes/evidência recente, o overview deve ser corrigido. Documentação histórica deve ser preservada como histórico, mas não deve prevalecer sobre o comportamento observado.
