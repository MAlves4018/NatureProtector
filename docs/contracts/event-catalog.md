# Event Catalog (V1)

## Objetivo

Documentar o catálogo mínimo de eventos e sinais operacionais da V1,
distinguindo o contrato RabbitMQ atual, camadas internas da prevenção e eventos
futuros.

## Regras desta versão

- Não altera contratos RabbitMQ.
- Não declara como vivo nenhum evento que não esteja publicado end-to-end.
- Usa estado `ativo`, `interno`, `parcial`, `futuro`.
- Quando houver conflito, prevalecem código, testes e evidência runtime recente.

## Catálogo de eventos e sinais

| Nome | Tipo | Produtor | Consumidor | Estado | Payload ou forma atual | Altera RabbitMQ agora | Leitura correta |
|---|---|---|---|---|---|---|---|
| `SensorReadingProduced` | Evento externo RabbitMQ | `Simulator.Host` (`RabbitMqReadingPublisher`) | `Prevention.Host`, fila principal e fila de observabilidade raw | Ativo | `EventEnvelope<SensorReadingProducedPayload>` | Não | Evento externo vivo da ingestão V1. |
| `EventEnvelope<SensorReadingProducedPayload>` | Contrato RabbitMQ atual | `Simulator.Host` | Consumidores que deserializam o envelope canónico | Ativo | `SchemaVersion`, `EventId`, `CorrelationId`, `Producer`, `EventType`, `AreaId`, `EventTime`, `IngestTime`, `Payload` | Não | Envelope e payload reais transportados pelo broker. |
| `OperationalEvent` | Camada interna | `Prevention.Host` / pipeline de risco | `NormalizedReading` e pipeline interna | Interno | Record interno criado a partir de `EventEnvelope<SensorReadingProducedPayload>` | Não | Adaptador interno; não é evento externo RabbitMQ. |
| `ReadingAccepted` | Semântica operacional | Pipeline/projeções | Persistência, evidência e futura evolução de eventos | Parcial | Hoje materializada por logs/persistência, não como publicação RabbitMQ completa | Não | Conceito útil; não apresentar como evento externo vivo. |
| `ReadingRejected` | Semântica operacional | Worker/inbox/pipeline | Persistência de rejeição/quarentena e evidência | Parcial | Hoje materializada por rejeições/quarentena, não como publicação RabbitMQ completa | Não | Conceito útil; não apresentar como evento externo vivo. |
| `ReadingNormalized` | Semântica operacional | Pipeline interna | Elegibilidade e scoring | Parcial | Hoje existe como `NormalizedReading` interno, não como evento externo publicado | Não | Camada interna/semântica; publicação formal fica futura. |
| `area-risk-high` | Código de alerta/projeção | Projection store (`InMemoryAreaOperationalProjectionStore` / `PostgresAreaOperationalProjectionStore`) | API/consulta de projeção | Ativo como código de projeção | `projection.alert_state` com mensagem `AlertState=<estado>` | Não | Código/estado persistido; não é evento formal. |
| `WarningRaised` | Evento formal futuro | Futuro | Futuro | Futuro | Payload a especificar quando houver publicação end-to-end | Não | Evolução possível de alertas; não está vivo na V1 atual. |
| `AlarmRaised` | Evento formal futuro | Futuro | Futuro | Futuro | Payload a especificar quando houver publicação end-to-end | Não | Evolução possível de alertas; não está vivo na V1 atual. |

## Notas de compatibilidade

- `SensorReadingProduced` e `EventEnvelope<SensorReadingProducedPayload>` são a fronteira externa atual.
- `OperationalEvent` não substitui o payload RabbitMQ. É um adaptador interno para reduzir acoplamento entre transporte e pipeline de risco.
- `ReadingAccepted`, `ReadingRejected` e `ReadingNormalized` devem ser tratados como semântica interna/parcial enquanto não houver publicação formal end-to-end.
- `WarningRaised` e `AlarmRaised` continuam eventos futuros.
- `area-risk-high` é código de alerta persistido em projeção. A API expõe `alertState` a partir dessa projeção e não recalcula risco.

## Itens futuros

| ID | Tema | Estado | Próxima ação |
|---|---|---|---|
| EVT-F01 | Publicação formal de `ReadingAccepted`, `ReadingRejected` e `ReadingNormalized` | Futuro | Definir contrato, routing keys, consumidores e migração. |
| EVT-F02 | Eventos formais `WarningRaised` e `AlarmRaised` | Futuro | Definir payloads e lifecycle quando os alertas deixarem de ser apenas projeção. |
| EVT-F03 | Evolução de `area-risk-high` para evento formal | Futuro | Separar código de projeção de evento operacional publicado, se necessário. |
