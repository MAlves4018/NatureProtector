# Event Catalog (V1)

## Objetivo

Documentar eventos atuais, parciais e futuros da V1, distinguindo conceito de domínio (`OperationalEvent`) da implementação atual (`EventEnvelope<TPayload>`).

## Regras desta versão

- Não altera contratos RabbitMQ.
- Não cria novos eventos no código.
- Usa estado `ativo`, `parcial`, `alvo`, `futuro`.

## Catálogo de eventos

| Nome | Produtor | Consumidor | Estado | Payload atual ou alvo | Relação com Proposal | Altera RabbitMQ agora | Prioridade de implementação |
|---|---|---|---|---|---|---|---|
| SensorReadingProduced | Simulator Host (`RabbitMqReadingPublisher`) | Prevention Host inbox/worker; observability raw queue | ativo | Atual: `EventEnvelope<TPayload>` com payload de leitura de sensor [CONFIRMAR tipo concreto] | Base de entrada de dados operacionais | não | Alta |
| EventEnvelope<TPayload> | Produtores que publicam eventos operacionais | Consumidores que deserializam envelope canónico | ativo | Atual: `SchemaVersion, EventId, CorrelationId, Producer, EventType, AreaId, EventTime, IngestTime, Payload` | Implementação atual de transporte de eventos | não | Alta |
| OperationalEvent (conceito V1) | [CONFIRMAR] | [CONFIRMAR] | alvo | Alvo: payload canónico de domínio sobre `EventEnvelope<TPayload>` | Contrato-alvo V1 para semântica operacional | não | Alta |
| ReadingAccepted | Prevention processing flow [CONFIRMAR ponto exato] | [CONFIRMAR] | alvo | Atual/alvo: envelope com tipo `ReadingAccepted` e routing key `ingestion.reading.accepted` | Marca aceitação semântica de leitura | não | Média |
| ReadingRejected | Prevention processing flow [CONFIRMAR ponto exato] | [CONFIRMAR] | parcial | Atual/alvo: envelope com tipo `ReadingRejected` e routing key `ingestion.reading.rejected` | Marca rejeição com motivo operacional | não | Média |
| ReadingNormalized | Prevention processing flow [CONFIRMAR ponto exato] | [CONFIRMAR] | alvo | Atual/alvo: envelope com tipo `ReadingNormalized` e routing key `ingestion.reading.normalized` | Marca saída normalizada para etapas seguintes | não | Média |
| WarningRaised | [CONFIRMAR] | [CONFIRMAR] | futuro | Alvo: evento formal de aviso operacional [CONFIRMAR campos] | Evolução de AlertState para gradação de alerta | não | Baixa |
| AlarmRaised | [CONFIRMAR] | [CONFIRMAR] | futuro | Alvo: evento formal de alarme operacional [CONFIRMAR campos] | Evolução de AlertState para incidentes críticos | não | Baixa |
| area-risk-high (alert code, não evento formal) | Projection store (`InMemoryAreaOperationalProjectionStore` / `PostgresAreaOperationalProjectionStore`) | API/consulta de projeção [CONFIRMAR] | parcial | Atual: código de alerta persistido em projeção, sem evento formal dedicado | Sinal atual parcial para estado de alerta | não | Média |

## Notas de compatibilidade

- `OperationalEvent` permanece conceito V1 até existir contrato canónico estável.
- `EventEnvelope<TPayload>` mantém-se como implementação ativa de transporte nesta fase.
- `ReadingAccepted`, `ReadingNormalized`, `WarningRaised` e `AlarmRaised` devem ser tratados como `alvo/futuro` quando não houver formalização end-to-end comprovada.

## Decisões pendentes

| ID | Pendência | Impacto | Próxima ação |
|---|---|---|---|
| EVT-P01 | Confirmar tipo concreto do payload de `SensorReadingProduced` | Médio | Inspecionar contrato de payload no módulo de simulação/mensageria |
| EVT-P02 | Confirmar consumidores formais de `ReadingAccepted/ReadingRejected/ReadingNormalized` | Alto | Levantar handlers/subscrições por routing key |
| EVT-P03 | Definir contrato mínimo de `OperationalEvent` (campos obrigatórios) | Alto | Proposta V1 de contrato + testes de compatibilidade |
| EVT-P04 | Definir payload alvo de `WarningRaised` | Médio | Especificação de evento sem alterar RabbitMQ |
| EVT-P05 | Definir payload alvo de `AlarmRaised` | Médio | Especificação de evento sem alterar RabbitMQ |
| EVT-P06 | Confirmar fronteira entre `area-risk-high` (código) e evento formal de alerta | Alto | Regra de tradução código->evento para fase posterior |
