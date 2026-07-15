# NatureProtector.Prevention.Host

Este projeto é o host de execução do módulo de prevenção. Ele já não é apenas o consumidor que calcula risco e escreve em InfluxDB: desde a fase 6 passou também a criar um primeiro ponto de commit durável no schema `pipeline`, desde a fase 7 passou a gerir novas tentativas internas e quarentena persistida, desde a fase 8 passou a atualizar a primeira projeção operacional por área, e na fase 9 passou a persistir leituras, assessments e snapshots em PostgreSQL e a atualizar projeção por célula.

## Fluxo ativo de execução

O caminho hoje ligado pelo `Program.cs` é este:

1. `PreventionWorker` cria ligação a RabbitMQ, declara a topologia base e aplica `prefetch` no canal do consumidor.
2. O worker consome da fila `np.ingestion.readings`.
3. O ponto de entrada só aceita hoje `SchemaVersion = 1.0`, `EventType = SensorReadingProduced`, envelope coerente e `OperationalState != Invalid`.
4. Se o payload não passar esta validação, o host regista uma rejeição em `pipeline.rejected_events`, faz `ack` e o evento não entra no fluxo aceite.
5. Se o envelope passar a validação, `IReadingEventInbox` persiste o evento em `pipeline.event_inbox`, cria a tentativa inicial em `pipeline.processing_attempts` e só depois disso o worker faz `ack`.
6. Se o evento for novo, o host delega o trabalho ao `ReadingEventProcessingService`.
7. Se o processamento terminar bem, a tentativa é marcada como concluída e o inbox passa a `Processed`.
8. Se o processamento falhar com erro retryable, o evento passa a `RetryPending`, fica com `NextAttemptNotBefore` e será retomado por `InboxRetryWorker`.
9. Se o processamento falhar com erro permanente ou esgotar a política de novas tentativas, o evento passa a `Quarantined` e fica registado em `pipeline.quarantined_events`.
10. O `InboxRetryWorker` também recupera eventos que ficaram em `Processing` para lá de `ProcessingLeaseTimeoutSeconds`; quando uma lease expirada é recuperada, uma finalização tardia da tentativa antiga é ignorada e não pode sobrescrever a tentativa corrente.
11. Cada assessment também atualiza `projection.cell_operational_state` para a célula do sensor que originou a leitura.
12. Depois de cada `AreaRiskSnapshot`, o host atualiza `projection.area_operational_state` e um alerta ativo simples por área.
13. A fotografia operacional da área é calculada a partir do último assessment conhecido por sensor, não do histórico completo da área.

## Ficheiros principais

- `Program.cs`
  - composição do host, configuração e escolha da inbox
- `Configuration/PreventionHostOptions.cs`
  - configuração da inbox durável, retries e `prefetch` do consumidor
- `PreventionWorker.cs`
  - consumo de RabbitMQ, validação de entrada, `ack` e ligação à inbox
- `Processing/ReadingEventProcessingService.cs`
  - execução do fluxo operacional com política de nova tentativa e quarentena
- `Processing/InboxRetryWorker.cs`
  - retoma tentativas já devidas e reprocessa eventos a partir da inbox
- `Processing/ReadingRiskPipeline.cs`
  - fluxo ativo de cálculo, escrita e atualização das projeções por área e por célula
- `Projection/IAreaOperationalProjectionStore.cs`
  - fronteira da projeção operacional
- `Projection/PostgresAreaOperationalProjectionStore.cs`
  - implementação durável do schema `projection`
- `Projection/InMemoryAreaOperationalProjectionStore.cs`
  - fallback em memória para testes e arranque simples
- `Processing/IReadingEventInbox.cs`
  - fronteira da inbox
- `Processing/PostgresReadingEventInbox.cs`
  - implementação durável sobre o schema `pipeline`
- `Processing/InMemoryReadingEventInbox.cs`
  - fallback para testes e arranque sem PostgreSQL
- `Persistence/InMemoryAcceptedReadingRepository.cs`
  - fallback em memória para leituras aceites
- `Persistence/PostgresAcceptedReadingRepository.cs`
  - persistência durável de leituras aceites no schema `projection`
- `Persistence/PostgresRiskAssessmentRepository.cs`
  - persistência durável de assessments no schema `projection`
- `Persistence/PostgresAreaRiskSnapshotRepository.cs`
  - persistência durável de snapshots no schema `projection`

## Configuração usada hoje

- secção `RabbitMq`
  - host, porta, credenciais e exchange
- secção `InfluxDb`
  - URL, token, organização e bucket
- secção `PreventionHost`
  - `PipelinePersistenceEnabled`
  - `ConsumerPrefetchCount`
  - `MaxProcessingAttempts`
  - `RetryDelaySeconds`
  - `RetryPollingIntervalSeconds`
  - `ProcessingLeaseTimeoutSeconds`

Quando `PipelinePersistenceEnabled = true`, o host usa o PostgreSQL como inbox durável, store dos logs operacionais e store das projeções operacionais.
Quando `PipelinePersistenceEnabled = false`, o host continua a arrancar com inbox, persistência operacional e projeções em memória.
`ConsumerPrefetchCount` limita quantas mensagens podem ficar em voo no canal RabbitMQ antes de materialização mínima no inbox. O valor por defeito fica baixo para priorizar estabilidade e tornar o backlog observável.

## Health e readiness

- `/health/live` prova apenas que o processo da Prevention está vivo e que o servidor HTTP continua a responder.
- `/health/ready` exige o consumer RabbitMQ ativo.
- `/health/ready` também exige PostgreSQL quando `PipelinePersistenceEnabled = true`.
- Quando `PipelinePersistenceEnabled = false`, a readiness não cria uma dependência artificial de PostgreSQL porque inbox, persistência operacional e projeções usam as implementações em memória.
- Uma indisponibilidade temporária de RabbitMQ ou PostgreSQL deve retirar o host de readiness sem o transformar numa falha de liveness; depois da recuperação das dependências, a readiness deve regressar automaticamente a `200`.

## Observabilidade operacional mínima

O host passa a emitir timings curtos e estruturados para o caminho do evento aceite, sem alterar a semântica do fluxo:

- `inbox_store_ms`
  - mede o custo da materialização mínima no inbox antes do `ack`
- `processing_total_ms`
  - mede o custo total de uma tentativa de processamento depois do inbox
- `accepted_reading_persist_ms`
- `accepted_reading_influx_ms`
- `risk_assessment_persist_ms`
- `save_cell_projection_ms`
- `risk_assessment_influx_ms`
- `get_latest_by_area_ms`
- `build_snapshot_ms`
- `snapshot_persist_ms`
- `snapshot_influx_ms`
- `save_area_projection_ms`
- `pipeline_total_ms`

Estes logs incluem `EventId` e `CorrelationId` para permitir correlação entre consumo, inbox, retries e pipeline de risco. O objetivo é tornar visível onde o tempo por evento está a ser gasto antes de qualquer otimização adicional.

No perfil local do repositório, os valores de `InfluxDb` podem vir da secção `InfluxDb` em `appsettings.json` ou do `.env` na raiz do workspace, seguindo o mesmo princípio já usado para a ligação PostgreSQL.

O perfil local suportado por defeito do repositório usa `PipelinePersistenceEnabled = true` em conjunto com o simulador ligado ao plano de controlo. Se quisermos correr uma demo standalone, devemos desligar também a persistência do fluxo operacional para evitar leituras de sensores que não existem no schema `control`.

## O que este host já fecha

- consumo real de eventos gerados pelo simulador;
- declaração de topologia RabbitMQ suficiente para o fluxo atual;
- commit durável do evento antes do `ack` do broker;
- registo de tentativas de processamento e rejeições técnicas;
- rejeição semântica precoce para `SchemaVersion` e `EventType` fora do contrato suportado e para leituras com `OperationalState = Invalid`;
- retries internos a partir da inbox;
- recuperação de leases de processamento expiradas sem permitir que finalizações tardias sobrescrevam a tentativa atual;
- quarentena persistida para falhas permanentes ou exaustão de retries;
- persistência durável das leituras aceites, assessments e snapshots;
- escrita de telemetria operacional em InfluxDB;
- cálculo simples de risco e agregação por área;
- agregação operacional baseada no último assessment por sensor;
- projeção operacional simples por área;
- projeção operacional simples por célula;
- alerta ativo simples por área;
- liveness process-only e readiness dependente do consumer RabbitMQ e, no modo persistente, de PostgreSQL.

## O que ainda não fecha

- validação cruzada precoce entre `AreaId` do envelope e o sensor do plano de controlo;
- publicação de `ReadingAccepted`, `ReadingRejected` ou `ReadingNormalized`;
- replay manual ou assistido de eventos em quarentena;
- alertas ricos com histerese, cooldown e acknowledgement.

## Relação com outros módulos

- consome contratos de `NatureProtector.Shared`;
- usa o scoring de `NatureProtector.Prevention`;
- usa `NatureProtector.Infrastructure.Influx` para persistência time-series;
- usa `NatureProtector.Infrastructure.Postgres` para a inbox, tracking durável e projeção operacional;
- espera eventos produzidos por `NatureProtector.Simulator.Host`.
