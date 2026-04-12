# NatureProtector.Prevention.Host

Este projeto é o host de execução do módulo de prevenção. Ele já não é apenas o consumidor que calcula risco e escreve em InfluxDB: desde a fase 6 passou também a criar um primeiro ponto de commit durável no schema `pipeline`, desde a fase 7 passou a gerir novas tentativas internas e quarentena persistida, desde a fase 8 passou a atualizar a primeira projeção operacional por área, e na fase 9 passou a persistir leituras, assessments e snapshots em PostgreSQL e a atualizar projeção por célula.

## Fluxo ativo de execução

O caminho hoje ligado pelo `Program.cs` é este:

1. `PreventionWorker` cria ligação a RabbitMQ e declara a topologia base.
2. O worker consome da fila `np.ingestion.readings`.
3. Se o payload não for um envelope válido, o host regista uma rejeição em `pipeline.rejected_events` e faz `ack`.
4. Se o envelope for válido, `IReadingEventInbox` persiste o evento em `pipeline.event_inbox`, cria a tentativa inicial em `pipeline.processing_attempts` e só depois disso o worker faz `ack`.
5. Se o evento for novo, o host delega o trabalho ao `ReadingEventProcessingService`.
6. Se o processamento terminar bem, a tentativa é marcada como concluída e o inbox passa a `Processed`.
7. Se o processamento falhar com erro retryable, o evento passa a `RetryPending`, fica com `NextAttemptNotBefore` e será retomado por `InboxRetryWorker`.
8. Se o processamento falhar com erro permanente ou esgotar a política de novas tentativas, o evento passa a `Quarantined` e fica registado em `pipeline.quarantined_events`.
9. Cada assessment também atualiza `projection.cell_operational_state` para a célula do sensor que originou a leitura.
10. Depois de cada `AreaRiskSnapshot`, o host atualiza `projection.area_operational_state` e um alerta ativo simples por área.
11. A fotografia operacional da área é calculada a partir do último assessment conhecido por sensor, não do histórico completo da área.

## Ficheiros principais

- `Program.cs`
  - composição do host, configuração e escolha da inbox
- `Configuration/PreventionHostOptions.cs`
  - ativa ou desativa a persistência durável da inbox
- `PreventionWorker.cs`
  - consumo de RabbitMQ, `ack` e ligação à inbox
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
  - `MaxProcessingAttempts`
  - `RetryDelaySeconds`
  - `RetryPollingIntervalSeconds`

Quando `PipelinePersistenceEnabled = true`, o host usa o PostgreSQL como inbox durável, store dos logs operacionais e store das projeções operacionais.
Quando `PipelinePersistenceEnabled = false`, o host continua a arrancar com inbox, persistência operacional e projeções em memória.

No perfil local do repositório, os valores de `InfluxDb` podem vir da secção `InfluxDb` em `appsettings.json` ou do `.env` na raiz do workspace, seguindo o mesmo princípio já usado para a ligação PostgreSQL.

O perfil local suportado por defeito do repositório usa `PipelinePersistenceEnabled = true` em conjunto com o simulador ligado ao plano de controlo. Se quisermos correr uma demo standalone, devemos desligar também a persistência do fluxo operacional para evitar leituras de sensores que não existem no schema `control`.

## O que este host já fecha

- consumo real de eventos gerados pelo simulador;
- declaração de topologia RabbitMQ suficiente para o fluxo atual;
- commit durável do evento antes do `ack` do broker;
- registo de tentativas de processamento e rejeições técnicas;
- retries internos a partir da inbox;
- quarentena persistida para falhas permanentes ou exaustão de retries;
- persistência durável das leituras aceites, assessments e snapshots;
- escrita de telemetria operacional em InfluxDB;
- cálculo simples de risco e agregação por área;
- agregação operacional baseada no último assessment por sensor;
- projeção operacional simples por área;
- projeção operacional simples por célula;
- alerta ativo simples por área.

## O que ainda não fecha

- validação semântica rica antes da prevenção;
- publicação de `ReadingAccepted`, `ReadingRejected` ou `ReadingNormalized`;
- replay manual ou assistido de eventos em quarentena;
- alertas ricos com histerese, cooldown e acknowledgement.

## Relação com outros módulos

- consome contratos de `NatureProtector.Shared`;
- usa o scoring de `NatureProtector.Prevention`;
- usa `NatureProtector.Infrastructure.Influx` para persistência time-series;
- usa `NatureProtector.Infrastructure.Postgres` para a inbox, tracking durável e projeção operacional;
- espera eventos produzidos por `NatureProtector.Simulator.Host`.
