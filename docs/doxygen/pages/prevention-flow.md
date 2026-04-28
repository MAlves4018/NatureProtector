@page prevention_flow Fluxo nominal da prevenção

# Fluxo nominal da prevenção

## Objetivo da página

Explicar como o `Prevention.Host` consome leituras simuladas, valida contratos, separa rejeição de processamento, calcula risco e atualiza persistência operacional.

## Âmbito

Esta página cobre o fluxo implementado no host de prevenção: consumo RabbitMQ, validação do envelope, inbox, política de retries e quarentena, pipeline de risco, projeções em PostgreSQL e escrita temporal em InfluxDB.

Não descreve deteção ou combate a incêndios como subsistemas autónomos. Também não afirma a existência de eventos normalizados publicados no broker; no fluxo atual o consumidor recebe `SensorReadingProduced` e materializa os resultados em persistência e observabilidade.

## Componentes principais

@ref NatureProtector.Prevention.Host.PreventionWorker é a fronteira com RabbitMQ. Declara a topologia, consome `np.ingestion.readings`, valida o envelope e decide se a mensagem é rejeitada antes da inbox ou se entra no fluxo operacional.

@ref NatureProtector.Prevention.Host.Processing.PostgresReadingEventInbox implementa deduplicação, tentativa de processamento, retry e quarentena em PostgreSQL quando `PipelinePersistenceEnabled=true`. @ref NatureProtector.Prevention.Host.Processing.ReadingEventProcessingService centraliza a política de sucesso, retry e quarentena, reutilizada tanto pelo consumidor como por @ref NatureProtector.Prevention.Host.Processing.InboxRetryWorker.

@ref NatureProtector.Prevention.Host.Processing.ReadingRiskPipeline é a sequência nominal da leitura aceite: persiste a leitura, escreve em InfluxDB, calcula avaliação de risco, atualiza estado por célula, calcula snapshot de área, escreve snapshot e atualiza projeção agregada e alertas simples.

## Fluxo nominal

\startuml
title Fluxo nominal da prevenção
autonumber
queue "RabbitMQ" as Rabbit
participant "PreventionWorker" as Worker
participant "IReadingEventInbox" as Inbox
participant "ReadingEventProcessingService" as Processing
participant "ReadingRiskPipeline" as Pipeline
database "PostgreSQL" as Postgres
database "InfluxDB" as Influx

Rabbit -> Worker : SensorReadingProduced
Worker -> Worker : desserializar e validar envelope
alt contrato inválido
  Worker -> Inbox : StoreRejectedAsync(...)
  Inbox -> Postgres : rejected_events
  Worker -> Rabbit : ack
else contrato válido
  Worker -> Inbox : StoreIncomingAsync(...)
  Inbox -> Postgres : event_inbox + processing_attempts
  Worker -> Rabbit : ack
  Worker -> Processing : ProcessAsync(envelope, lease)
  Processing -> Pipeline : ProcessAcceptedReadingAsync(...)
  Pipeline -> Postgres : accepted_reading_log
  Pipeline -> Influx : accepted_readings
  Pipeline -> Postgres : risk_assessment_log
  Pipeline -> Postgres : cell_operational_state
  Pipeline -> Influx : risk_assessments
  Pipeline -> Postgres : area_risk_snapshot_log
  Pipeline -> Influx : area_risk_snapshots
  Pipeline -> Postgres : area_operational_state + alert_state
  Processing -> Inbox : CompleteProcessingAsync(...)
  Inbox -> Postgres : marcar processed
end
\enduml

O ponto mais importante é a fronteira de entrega. O `ack` ao broker acontece depois da materialização na inbox, antes do processamento completo da pipeline. Se o processamento falhar depois do `ack`, a recuperação passa pelo estado persistido da inbox e pelo worker de retries.

## Modelo de retries e quarentena

\startuml
title Estados de processamento na inbox
[*] --> Processing
Processing --> Processed : pipeline concluída
Processing --> RetryPending : falha recuperável e tentativas disponíveis
RetryPending --> Processing : InboxRetryWorker inicia nova tentativa
Processing --> Quarantined : falha permanente
Processing --> Quarantined : tentativas esgotadas
RetryPending --> Quarantined : envelope persistido inválido
Processed --> [*]
Quarantined --> [*]
\enduml

As mensagens com JSON inválido, envelope nulo, schema não suportado, tipo de evento não suportado, identificadores obrigatórios em falta, coordenadas inválidas ou `OperationalState=Invalid` são rejeitadas antes da inbox de processamento. Essas rejeições ficam registadas como rejeições técnicas, mas não seguem para scoring.

## Decisões importantes

- O consumidor usa `ack` manual e `ConsumerPrefetchCount` para limitar trabalho não materializado.
- A inbox trata `EventId` como chave lógica de deduplicação.
- Duplicados com o mesmo payload não são reprocessados; duplicados com payload divergente geram rejeição associada.
- A classificação de falhas decide entre retry e quarentena; `TimeoutException` e alguns erros transitórios de base são recuperáveis, enquanto falhas permanentes são quarantinadas.
- InfluxDB recebe séries temporais para observabilidade; PostgreSQL continua a ser a persistência operacional consultável.

## Estado atual e limitações

O estado implementado suporta validação de contrato, rejeição antes da inbox, inbox durável, retries agendados, quarentena, logs de leitura aceite, logs de avaliação de risco, snapshots de área, projeções por célula e por área, e alertas simples `area-risk-high`.

As limitações conhecidas incluem a ausência de uma DLQ RabbitMQ própria, ausência de replay manual pela API, ausência de semântica publicada como eventos `accepted/rejected/normalized`, e política de alerta ainda simples. Se o host for interrompido depois de um evento ficar em `Processing` e antes de ser marcado como `RetryPending`, `Processed` ou `Quarantined`, a recuperação automática desse estado intermédio não está documentada como capacidade completa.

## Pontos do repositório a consultar

- `src/NatureProtector.Prevention.Host/Program.cs`
- `src/NatureProtector.Prevention.Host/PreventionWorker.cs`
- `src/NatureProtector.Prevention.Host/Processing/PostgresReadingEventInbox.cs`
- `src/NatureProtector.Prevention.Host/Processing/ReadingEventProcessingService.cs`
- `src/NatureProtector.Prevention.Host/Processing/InboxRetryWorker.cs`
- `src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs`
- `src/NatureProtector.Prevention.Host/Projection/PostgresAreaOperationalProjectionStore.cs`

## Ligações para páginas relacionadas

- Para a origem dos eventos produzidos, consultar @ref simulator_flow.
- Para os schemas que guardam inbox, logs e projeções, consultar @ref persistence_model.
- Para testes sobre rejeição, ack, retry e quarentena, consultar @ref tests_as_documentation.
