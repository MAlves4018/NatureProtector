# Contrato runtime RabbitMQ — topologia, papéis e entrega

## Estado

```text
TARGET_CONTRACT
IMPLEMENTED_NOT_PROVED_PHASE3F
```

Este documento define o alvo normativo para a remediação RabbitMQ. O estado
executável atual continua descrito pela auditoria e pelos testes de
caracterização da Fase 1 até a implementação passar os gates.

## Recursos

| Recurso | Papel | Necessário | Consumer esperado | Pode bloquear publish | Retenção |
|---|---|---:|---:|---:|---|
| `np.events` | exchange operacional | sim | n/a | n/a | n/a |
| `np.ingestion.readings` | `PrimaryWorkQueue` | sim | sim | sim, fail-closed | limitada por ambiente e monitorizada |
| `np.observability.raw` | `AuxiliaryDiagnosticQueue` | não | não obrigatório | **não** | TTL + cap + `drop-head` |

Routing key V1:

```text
simulation.reading.produced
```

## Invariantes

### Primary

1. A binding da ingestion existe antes de o publisher ficar operacional.
2. Ausência da binding, queue ou consumer é uma falha operacional.
3. A fila participa na readiness da Prevention e no scaling.
4. A policy e capacidade efetivas são observáveis.
5. Publisher confirm não é confundido com processamento concluído.

### Auxiliary

1. Desativada por omissão.
2. Sem binding quando desativada.
3. Quando ativada, tem retenção finita.
4. `overflow=reject-publish` é proibido.
5. Não participa no scaling nem na readiness funcional.
6. Perda por `drop-head` é esperada e documentada.
7. Não é evidence completa nem arquivo de auditoria.

## Semântica de publicação

O sucesso do publish significa apenas que o broker confirmou a publicação de
acordo com a topologia disponível. Não significa:

- que a ingestion processou a mensagem;
- que PostgreSQL persistiu os efeitos;
- que todas as filas ligadas aceitaram atomicamente;
- que uma fila auxiliar preservou a cópia.

`mandatory=true` deteta ausência total de destinos routable. Não deteta ausência
específica da fila principal quando outro destino continua ligado.

A prova end-to-end exige correlation por `SourceEventId`/`SimulationRunId` no
inbox e nas projeções.

## Declaração

Todos os processos declaradores obtêm bindings efetivas do mesmo contrato de
opções. É proibido manter arrays hardcoded diferentes em:

- Simulator publisher;
- controlled-validation publisher;
- Prevention;
- Runtime observability.

## Migração

Recursos duráveis não desaparecem quando a configuração deixa de os declarar.
Qualquer alteração de queue/binding exige:

- inventário before;
- dry-run;
- mudança de policy segura;
- rollout de todas as revisões;
- unbind explícito;
- observação;
- decisão de backlog;
- evidence before/after;
- rollback documentado.

## Observabilidade mínima

Cada queue expõe:

```text
queueName
queueRole
requiredConsumer
blocksPipeline
messagesReady
messagesUnacknowledged
messagesTotal
messageBytesReady
consumers
policy
messageTtlMilliseconds
maxLength
maxLengthBytes
overflow
capacityPercent
collectionStatus
observedAt
```

## Compatibilidade

Este contrato não altera o envelope V1 nem a routing key. A remediação é de
topologia, retenção, health e observabilidade.


## Implementação Phase 3F

A proposta Phase 3F materializa o contrato de migração sem executar qualquer
operação cloud:

- a policy declarativa default deixa de usar `^np\.` e passa a aplicar
  `reject-publish` apenas a `^np\.ingestion\.readings$`;
- cloud API, Prevention e Simulator Job declaram explicitamente
  `RabbitMq__ObservabilityRawEnabled=false`;
- `Invoke-RabbitMqRawQueueMigration.ps1` suporta inventário, plano, proteção,
  retirement da policy larga, unbind, verify e rollback;
- mutações exigem `-Apply`, `ShouldProcess` e confirmação exata;
- TTL e cap da raw são inputs obrigatórios por ambiente;
- purge e delete da queue não são automatizados;
- `Get-G81RabbitMqRawQueueMigrationInventory.ps1` recolhe apenas evidence
  read-only e não altera kubeconfig.

O estado permanece `IMPLEMENTED_NOT_PROVED_PHASE3F` até build, validadores,
reprodução local e rollout staging produzirem evidence.

## Contrato Phase 3G — confirmação ambígua e idempotência

### Classificação do resultado da publicação

| Sinal | Exceção | Certeza |
|---|---|---|
| `basic.return` matching | `RabbitMqUnroutableMessageException` | nenhuma fila recebeu |
| nack, timeout ou falha de confirm sem return matching | `RabbitMqPublishOutcomeUnknownException` | `UnknownPossiblePartialDelivery` |

A segunda classe nunca pode ser descrita como “mensagem não entregue”. A fila
principal pode já ter aceite e processado o EventId.

### Regra de retry

Um retry do mesmo evento lógico deve reutilizar o mesmo envelope e o mesmo
`EventId`/`MessageId`. Alterar o EventId cria um segundo evento lógico e não é
protegido pela deduplicação do inbox.

### Resultado do processo Simulator

Qualquer falha não tratada de publicação:

- persiste `SimulationRunStatus.Failed` e `EndedAt` quando a run estava Running;
- escreve o tipo de falha e `PossiblePartialDelivery` nos logs;
- termina o processo com exit code não zero.

### Prova de idempotência delimitada

Para o caso de entrega parcial seguido do retry do mesmo EventId, a prova exige:

- uma row `InboxEvents`;
- uma tentativa `Succeeded`;
- uma accepted reading;
- uma risk assessment;
- um area snapshot;
- uma projeção corrente de célula e uma de área.

O contrato não afirma exactly-once global nem cobre ainda uma falha entre a
escrita da projeção e a conclusão do inbox.

Estado: `IMPLEMENTED_NOT_PROVED_PHASE3G`.


### Controlled Validation

The controlled-validation runner uses the same non-zero process outcome and the
orchestrator marks a registered Running simulation run as Failed when message
publication fails. This prevents a controlled-validation publish failure from
leaving `EndedAt = NULL`.
