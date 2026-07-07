# Observabilidade e evidence runtime

Última atualização: 2026-07-05

Este documento descreve a fatia atual de observabilidade interna. É evidence runtime para um protótipo técnico, não validação científica de risco de incêndio, alerting oficial ou previsão calibrada.

## Modelo de health

A Backoffice mantem endpoints tecnicos simples para probes locais:

```text
GET /health
GET /health/live
GET /health/ready
```

Estes endpoints usam ASP.NET health checks simples e nao substituem o health operacional detalhado.

O health operacional detalhado é exposto por:

```text
GET /api/control/runtime/observability/health
```

O endpoint é autenticado com as roles existentes `Sim`, `Pipeline` ou `Admin`. Não acrescenta roles nem claims.

O estado de componente é explícito:

```text
Healthy
Degraded
Unhealthy
Unknown
AuthRequired
NotInstrumented
NotApplicable
```

A ausência de erros não é tratada como `Healthy`. Sinais ausentes ou inacessíveis são representados como `Unknown`, `NotInstrumented` ou `NotApplicable`. Endpoints alcançáveis que exigem autenticação são representados como `AuthRequired`.

Componentes atuais:

- `Backoffice.Api`: sinal positivo quando o pedido autenticado chega ao controller.
- `PostgreSQL`: probe de conectividade EF Core.
- `RabbitMQ`: RabbitMQ Management HTTP API e estado relevante de filas.
- `Prevention.Host`: sinal proxy a partir de consumers em `np.ingestion.readings`.
- `Simulator.Host`: ciclo de vida da última simulation run; uma run concluída é `NotApplicable`, não unhealthy.
- `InfluxDB`: probe HTTP de health; probes unauthorized são `AuthRequired` e probes unreachable são `Unknown`.
- `Grafana`: `/api/health` com estado da base de dados quando disponível.

## Métricas RabbitMQ

As métricas de filas RabbitMQ são expostas por:

```text
GET /api/control/runtime/observability/rabbitmq
```

Por fila:

```text
QueueName
MessagesReady
MessagesUnacknowledged
MessagesTotal
Consumers
ObservedAt
Source
CollectionStatus
Limitation
```

O endpoint usa a RabbitMQ Management HTTP API. Não expõe credenciais.

Métricas indisponíveis são nullable e marcadas com `CollectionStatus`. Não são convertidas para zero. Um valor zero significa que RabbitMQ reportou zero.

O backlog é reportado explicitamente como contagens ready, unacknowledged e total. A UI v2 não colapsa estes valores num único número ambíguo de backlog.

## Timestamps e correlação

O contrato RabbitMQ publicado continua a ser:

```text
EventEnvelope<TPayload>
SensorReadingProduced
```

Contém `EventTime`, `IngestTime` opcional, `EventId` e `CorrelationId`. Não contém `PublishedAt` persistido.

Isto significa que a latência publish-to-end continua bloqueada. O sistema pode mostrar timestamps persistidos de run/inbox/processing/risk, mas não deve afirmar latência integral por evento até existirem timestamps comparáveis de publicação, receção e processamento.

## Audit e timings por run

Audit por run:

```text
GET /api/control/runtime/runs/{runId}/audit
```

Timings por run:

```text
GET /api/control/runtime/runs/{runId}/timings
```

Ambos continuam a ler apenas registos runtime persistidos. Não recalculam risco.

As respostas incluem agora metadata opcional `dataScope`:

```text
RequestedRunId
ResolvedRunId
DataRunId
ObservedAt
Source
Scope
Limitations
```

Os timings também incluem uma `timeline` ordenada de pontos persistidos medidos quando disponíveis:

```text
requested
started
first_received
first_processing_started
first_risk_assessment
first_alert
last_processing_finished
completed
```

Só são incluídos pontos persistidos medidos. Durações stopwatch em logs continuam a ser logs até existir um modelo estruturado de persistência de run timing.

## Qualidade e classificadores

Os dados atuais de audit de qualidade continuam parciais:

- o resumo de quality flags é derivado de estados operacionais persistidos de accepted readings e aritmética de missing events;
- o resumo de eligibility é derivado de explanation summaries persistidos de risk assessments e diferenças entre contagem accepted/risk;
- payloads detalhados de classifier não são persistidos como projeções runtime agregadas.

Nenhuma semântica de scoring, eligibility, `Blocked`, `PartialButUsable`, `CompleteEligible`, quality flag ou classifier foi alterada.

A persistência detalhada de classifier/quality continua a exigir owner review porque implica decisões aditivas de schema, retenção e tamanho de payload.

## Evidence HTTP

A evidence é exposta através de um catálogo HTTP allowlisted:

```text
GET /api/control/runtime/observability/evidence
GET /api/control/runtime/observability/evidence/{evidenceId}
```

Regras:

- a origem é limitada a `docs/evidence`;
- identificadores públicos são evidence IDs gerados, não paths de filesystem;
- evidence IDs são limitados aos caracteres do identificador gerado e valores reservados/path-like são rejeitados antes do lookup de conteúdo;
- extensões são allowlisted para `.md`, `.txt`, `.json` e `.csv`;
- o catálogo devolve os 250 ficheiros allowlisted mais recentes e reporta `evidence_catalog_truncated` quando existem mais;
- o conteúdo é limitado a 1 MiB;
- validação de path canónico impede traversal para fora de `docs/evidence`;
- a enumeração recursiva do catálogo ignora filesystem reparse points/symlinks;
- respostas usam `no-store`;
- a pasta Brain, `.env`, `.git`, paths arbitrários e ficheiros binários não são expostos.

A UI v2 descarrega runtime evidence allowlisted através do cliente API autenticado existente, por isso o mesmo caminho bearer/session usado pelo resto da app é aplicado a `GET /api/control/runtime/observability/evidence/{evidenceId}`. Não usa um anchor simples e não autenticado para `/api`.

## UI Pipeline

A UI v2 Pipeline consome os novos contratos de observabilidade de forma proporcional:

- service health aparece como campos técnicos;
- métricas RabbitMQ ready/unacknowledged/consumer aparecem apenas quando medidas;
- valores de fila indisponíveis continuam indisponíveis, não zero;
- publisher timestamps continuam `NotInstrumented`;
- projeções atuais/globais continuam marcadas como projeções atuais quando não há garantia de scope por run;
- run audit e timings continuam preferidos para detalhes da run selecionada;
- download de evidence é exposto apenas para itens de catálogo que reportam conteúdo HTTP disponível.

## Grafana e InfluxDB

Esta passagem não criou dashboards Grafana. Grafana health é verificado através do endpoint real de health e pode ser mostrado no operational health.

InfluxDB health é verificado através de HTTP. Se o endpoint local exigir autorização e a Backoffice não tiver token configurado, o componente fica `AuthRequired`, não `Healthy` nem falha runtime.

## Evidence de validação

Validação focada de observabilidade em 2026-06-18:

```powershell
dotnet test .\tests\NatureProtector.Shared.Tests\NatureProtector.Shared.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~HostTelemetryTests"
dotnet test .\tests\NatureProtector.Backoffice.Api.Tests\NatureProtector.Backoffice.Api.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~RuntimeObservabilityServiceTests|FullyQualifiedName~UnavailableRuntimeObservabilityServiceTests|FullyQualifiedName~ControlPlaneApiTests.RuntimeObservability|FullyQualifiedName~RuntimeEvidenceHttpSecurityTests"
```

Resultados:

- `HostTelemetryTests`: 4 passaram.
- testes focados de runtime observability e evidence HTTP da Backoffice: 18 passaram.

Esta passagem focada valida startup do OpenTelemetry host wiring sem infraestrutura externa de telemetry, estados explícitos de runtime observability indisponível, parsing de métricas RabbitMQ Management API através de HTTP client fake, autorização em endpoints de observability e segurança de evidence HTTP. Não é prova de entrega para collector real, prova de integração com broker RabbitMQ, prova de dashboard Grafana ou prova de latência integral por evento.

Comandos de validação anteriores executados na passagem de 2026-06-16:

```powershell
dotnet test tests\NatureProtector.Backoffice.Api.Tests\NatureProtector.Backoffice.Api.Tests.csproj --no-restore
dotnet test NatureProtector.sln --no-build --no-restore -m:1
npm run typecheck
npm test -- src/app/ui-v2/technicalSurfaces.test.ts
npm test -- src/app/ui-v2 src/app/services/api.test.ts
npm run test:coverage -- src/app/ui-v2 src/app/services/api.test.ts
```

Runtime smoke evidence foi capturada em:

```text
NatureProtector.brain/control/OBSERVABILITY-AND-RUNTIME-EVIDENCE-001/
```

Runtime smoke observado em 2026-06-14:

- `Backoffice.Api=Healthy`
- `PostgreSQL=Healthy`
- `RabbitMQ=Degraded`
- `Prevention.Host=Healthy`
- `Simulator.Host=NotApplicable`
- `InfluxDB=Unknown` na observação histórica de 2026-06-14; passagens atuais devem usar `AuthRequired` quando o endpoint responder `401`.
- `Grafana=Healthy`
- `np.ingestion.readings`: ready `0`, unacknowledged `0`, total `0`, consumers `1`
- `np.observability.raw`: ready `52`, unacknowledged `0`, total `52`, consumers `0`
- o catálogo de evidence devolveu os 250 itens allowlisted mais recentes e marcou `evidence_catalog_truncated`
- o catálogo de evidence devolveu conteúdo HTTP para um item allowlisted e rejeitou traversal com `400`

## Limitações restantes

- Sem `PublishedAt` RabbitMQ sem trabalho de contrato/instrumentação aprovado pelo owner.
- Ainda sem persistência detalhada de payloads classifier ou persistência agregada de projeção de quality.
- Esta passagem não criou dashboard Grafana.
- Não há claim de latência integral por evento.
- Runs históricas anteriores a futura persistência de quality/classifier continuarão sem evidence detalhada de classifier.
- `np.observability.raw` pode mostrar backlog com zero consumers por desenho; esse backlog é limitação diagnóstica não bloqueante quando `np.ingestion.readings` tem consumer e sem backlog.
