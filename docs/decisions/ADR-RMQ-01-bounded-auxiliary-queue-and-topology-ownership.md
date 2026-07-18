# ADR RMQ-01 — Fila raw auxiliar limitada e ownership da topologia

## Estado

**Aceite e implementado como proposta Phase 3F; ainda não provado no workspace owner nem aplicado em cloud.**

```text
RMQ_AUXILIARY_QUEUE_CONTRACT_ACCEPTED
IMPLEMENTED_NOT_PROVED_PHASE3F
CLOUD_MUTATION_NOT_AUTHORIZED_BY_THIS_ADR
```

## Contexto

A baseline declara o exchange `np.events` e liga sempre as filas
`np.ingestion.readings` e `np.observability.raw` à routing key
`simulation.reading.produced`.

A fila principal é consumida por `PreventionWorker`. A fila raw não tem consumer
operacional. A configuração `RabbitMq__ObservabilityRawEnabled=false` aparece
em Dockerfiles e Compose, mas não existe em `RabbitMqOptions` e não condiciona a
topologia.

Em local/Compose, nenhuma retenção é aplicada pelo projeto. Em cloud, a policy
`^np\.` aplica `max-length-bytes=1073741824` e `overflow=reject-publish` às duas
filas. Uma fila auxiliar cheia pode, portanto, gerar `basic.nack` ao publisher,
mesmo quando a fila principal já aceitou a reading.

Existe ainda drift documental: um documento histórico de G1 afirma que a fila
raw já é opcional, desativada por defeito e limitada com `drop-head`, mas esse
estado não corresponde ao código desta baseline.

## Decisão

### 1. Papéis das filas

A topologia distingue explicitamente:

- **PrimaryWorkQueue** — `np.ingestion.readings`;
- **AuxiliaryDiagnosticQueue** — `np.observability.raw`.

A fila principal:

- é necessária para o fluxo funcional;
- exige consumer ativo para readiness da Prevention;
- pode usar retenção fail-closed, incluindo `reject-publish`, desde que a
  capacidade seja monitorizada e provada;
- é o único backlog usado para scaling da Prevention.

A fila auxiliar:

- não integra o contrato funcional de entrega;
- fica desativada por omissão;
- só pode ser ativada por configuração explícita;
- não exige consumer quando for usada como buffer diagnóstico limitado;
- nunca pode bloquear ou fazer falhar a publicação principal;
- exige TTL, limite de mensagens ou bytes e `drop-head`;
- não participa no scaling da Prevention;
- não pode ser apresentada como arquivo durável ou evidence completa.

### 2. Configuração

`RabbitMqOptions` passa a ter uma opção real:

```text
ObservabilityRawEnabled=false
```

Quando `false`, nenhum processo pode declarar ou ligar a raw.

Quando `true`, a configuração deve ser validada antes do arranque. A retenção
numérica continua a ser definida por ambiente, mas deve obedecer a estes
invariantes:

```text
MessageTtlMilliseconds > 0
MaxLength > 0 OR MaxLengthBytes > 0
Overflow = drop-head
```

Não se fixa ainda um número universal, porque a baseline não contém evidence de
tamanho médio de mensagem, taxa sustentada ou orçamento de retenção. O ambiente
de staging deve medir esses valores antes de promover uma configuração para
produção.

### 3. Ownership durante a remediação

Para limitar o scope, a topologia funcional continua temporariamente declarada
pelas aplicações, mas com uma única fonte de decisão em `RabbitMqOptions` e num
helper partilhado. Simulator, controlled-validation publisher e Prevention não
podem manter listas independentes de bindings.

A infraestrutura continua responsável por:

- tipo de fila por ambiente;
- policies de retenção;
- permissões;
- TLS e credenciais;
- observabilidade da capacidade.

Uma migração futura para recursos do Messaging Topology Operator pode ser
avaliada separadamente. Não é necessária para corrigir este defeito.

### 4. Bindings duráveis existentes

Desativar a flag no código não remove uma binding durável já existente.

O rollout deve incluir um passo explícito e auditável:

1. inventário read-only;
2. proteção da raw com policy segura;
3. rollout de todas as revisões declaradoras;
4. remoção da binding antiga;
5. observação;
6. decisão sobre backlog;
7. delete opcional da queue.

O unbind nunca deve correr enquanto uma revisão antiga do Simulator,
controlled-validation publisher ou Prevention puder voltar a declarar a raw.

### 5. Semântica de publisher confirms

`mandatory=true` e publisher confirms não provam que a fila principal recebeu a
mensagem; provam apenas routing para pelo menos um destino e confirmação do
broker.

A mitigação mínima aceite é:

- a aplicação declara a binding principal durante o arranque;
- a raw fica desativada por omissão;
- um verificador de topologia confirma a binding principal antes/depois do
  rollout;
- a observabilidade classifica ausência da primary binding como falha;
- testes de caracterização preservam os casos de routing parcial.

Não se afirma atomicidade entre múltiplas filas.

## Consequências

### Positivas

- a fila auxiliar deixa de poder bloquear o pipeline central;
- a configuração passa a ter efeito observável;
- o contrato distingue trabalho funcional de diagnóstico;
- o rollout trata recursos duráveis existentes;
- a observabilidade e o scaling deixam de depender de nomes hardcoded.

### Custos e limitações

- é necessário migrar bindings já existentes;
- uma raw ativada com `drop-head` perde mensagens antigas por desenho;
- os valores de retenção precisam de evidence por ambiente;
- publisher confirms continuam sem garantir processamento end-to-end;
- revisões antigas podem reintroduzir drift se não forem retiradas.

## Alternativas rejeitadas

- manter a raw sempre ligada;
- criar imediatamente um novo consumer apenas para justificar a fila;
- usar `reject-publish` na fila auxiliar;
- tratar a raw como evidence completa;
- migrar toda a topologia para CRDs na mesma correção;
- introduzir outbox antes de provar falha transacional ou idempotente.

## Critérios de aceitação da implementação

- `ObservabilityRawEnabled=false` produz apenas a binding da ingestion;
- os três declaradores obedecem ao mesmo contrato;
- uma binding durável anterior é detetada e removida por rollout explícito;
- raw ativada exige retenção limitada e `drop-head`;
- backlog raw não altera readiness nem scaling da Prevention;
- absence da primary binding é observável como falha;
- os testes da Fase 1 são executados antes e depois da correção.


## Nota de implementação Phase 3F

A policy cloud generalista `natureprotector-quorum` é substituída no manifesto
por `natureprotector-primary-work-queue`, com pattern exato da fila principal.
A policy raw não recebe números universais no manifesto: o script de migração
exige TTL e `max-length-bytes` explicitamente aprovados para o ambiente e aplica
`drop-head` com prioridade superior durante a janela de migração.

A retirada da policy antiga e o unbind são ações independentes e confirmadas.
O script recusa o unbind se a binding principal estiver ausente, se existirem
consumers raw inesperados ou se uma raw existente não estiver protegida por uma
policy segura. O rollback não cria queues nem remove policies; apenas restaura a
binding quando a raw continua limitada.

## Nota de implementação Phase 3G

A Phase 3G torna explícita a incerteza de publisher confirms quando múltiplas
filas recebem a mesma publicação:

- `RabbitMqUnroutableMessageException` representa um `basic.return` matching e
  prova que a mensagem não foi encaminhada para nenhuma fila;
- `RabbitMqPublishOutcomeUnknownException` representa nack, timeout ou falha de
  confirmação sem `basic.return` matching e declara possível entrega parcial;
- a exceção inclui MessageId, exchange, routing key, primary queue e certeza de
  entrega;
- retries externos têm de preservar o mesmo EventId/MessageId;
- uma falha não tratada marca a run `Failed` e o processo Simulator com exit
  code não zero;
- testes Docker isolados provam que uma primary pode processar a mensagem mesmo
  quando o publisher recebe nack, e que reenviar o mesmo EventId não duplica os
  efeitos persistentes.

Isto não cria atomicidade entre queues, não introduz outbox e não sustenta um
claim de exactly-once. O estado permanece `IMPLEMENTED_NOT_PROVED_PHASE3G` até
build e execução dos testes unitários e Docker no workspace real.


### Controlled Validation

The controlled-validation runner uses the same non-zero process outcome and the
orchestrator marks a registered Running simulation run as Failed when message
publication fails. This prevents a controlled-validation publish failure from
leaving `EndedAt = NULL`.
