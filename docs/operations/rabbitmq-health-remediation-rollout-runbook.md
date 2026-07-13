# Runbook — rollout da remediação RabbitMQ e health/readiness

## Estado

**Plano de integração e execução. Não autoriza mutação cloud por si só.**

## Objetivo

Integrar a remediação sem perder mensagens silenciosamente, sem permitir que uma
revisão antiga recrie a binding raw e sem confundir probes verdes com prova
end-to-end.

## 1. Pré-condições

- Fase 1 integrada e executada no ambiente owner;
- evidence dos seis markers preservada;
- ADR RMQ-01 e HEALTH-01 aceites;
- scope de ficheiros da implementação revisto;
- staging antes de produção;
- janela e rollback definidos;
- nenhuma operação concorrente de rollout/teardown.

## 2. Inventário read-only before

Guardar em diretório timestamped:

```text
queues.json
bindings.json
consumers.json
policies.json
rabbitmqcluster.yaml
api-service.json
simulator-job.json
prevention-deployment.yaml
health-before.json
```

Confirmar:

- tipo das filas;
- depth e bytes;
- consumers;
- policy efetiva;
- binding `np.events -> np.observability.raw`;
- revisões/jobs antigos;
- endpoints live/ready atuais.

## 3. Ordem de integração de código

1. options e bindings condicionais;
2. testes unitários dos três declaradores;
3. policy distinta por papel;
4. Backoffice readiness PostgreSQL;
5. Prevention readiness PostgreSQL e transições RabbitMQ;
6. Management HTTPS/CA;
7. observabilidade por queue role;
8. script de migração com dry-run.

Não executar unbind antes de todas as imagens/revisões declaradoras usarem o
novo contrato.

## 4. Proteção temporária da raw

Antes do unbind, a raw existente deve deixar de ter `reject-publish`.

A policy específica deve:

- ter prioridade superior à policy geral;
- usar pattern exato;
- aplicar TTL e cap finitos;
- usar `drop-head`;
- ser validada por leitura depois da aplicação.

Os valores numéricos são aprovados por ambiente com base em message size e
janela pretendida. Não copiar um número de staging para produção sem evidence.

## 5. Rollout de aplicações

Confirmar novas revisões de:

- Simulator Job;
- Prevention;
- controlled-validation path;
- Backoffice API.

Gate:

```text
ObservabilityRawEnabled=false
```

em todas as revisões ativas, salvo campanha explícita que a ative.

## 6. Unbind

Executar primeiro `dry-run` que mostre exatamente:

```text
source exchange
routing key
destination queue
vhost
cluster/context
```

Depois remover apenas:

```text
np.events
  --simulation.reading.produced-->
np.observability.raw
```

Não apagar a queue na mesma ação.

## 7. Prova pós-unbind

Executar run pequena e guardar correlation:

1. Simulator termina com sucesso;
2. ingestion recebe e drena;
3. Prevention mantém consumer;
4. inbox contém os eventos esperados;
5. PostgreSQL contém efeitos associados à run;
6. raw não cresce;
7. nenhum `basic.nack`, `basic.return` ou channel close;
8. observabilidade mostra primary healthy e auxiliary disabled/not applicable.

## 8. Fault injection de health

### Backoffice

- DB disponível: live 200, ready 200;
- DB down: live 200, ready 503;
- DB recovered: ready 200;
- Management down: ready 200, collection degraded.

### Prevention

- Rabbit + DB disponíveis: live 200, ready 200;
- DB down com persistence ativa: live 200, ready 503;
- DB recovered: ready 200;
- consumer perdido: ready 503;
- consumer recuperado: ready 200.

## 9. TLS Management

Provar:

- URI `https://...:15671/api/queues`;
- CA correta;
- CA errada falha;
- hostname mismatch falha;
- CA inexistente falha cedo;
- credenciais não aparecem em evidence.

## 10. Backlog antigo

Escolher uma opção explícita:

- exportar amostra bounded e purgar;
- deixar expirar pela TTL;
- purgar diretamente com autorização;
- manter temporariamente com justificação e prazo.

Nunca classificar a raw como evidence completa.

## 11. Delete opcional

Apagar a raw apenas se:

- binding removida;
- zero consumers;
- backlog resolvido;
- nenhum workflow/job antigo a recria;
- owner aprova;
- rollback não depende da queue.

## 12. Rollback

Rollback aplicacional:

- restaurar revisão anterior apenas se a policy raw já impedir
  `reject-publish`;
- considerar que a revisão anterior pode recriar a binding;
- verificar bindings imediatamente depois;
- não reativar raw sem retenção segura.

Rollback de health:

- não mascarar DB down para obter probes verdes;
- corrigir timeout/configuração, não retirar a dependência funcional.

## 13. Critério de conclusão

```text
RAW_DISABLED_AND_UNBOUND
PRIMARY_QUEUE_PROVED
NO_AUXILIARY_PUBLISH_BLOCKING
BACKOFFICE_READY_DEPENDENCY_PROVED
PREVENTION_READY_DEPENDENCIES_PROVED
MANAGEMENT_TLS_PROVED
STAGING_END_TO_END_PROVED
```

## 14. Comandos Phase 3F

### 14.1 Inventário cloud read-only

Este passo não executa `get-credentials`, não altera kubeconfig e não lê
conteúdo de secrets:

```powershell
pwsh -NoProfile -File `
  .\scripts\cloud\Get-G81RabbitMqRawQueueMigrationInventory.ps1 `
  -ProjectId <STAGING_PROJECT_ID> `
  -Region europe-southwest1 `
  -Namespace natureprotector-staging
```

Gates antes de qualquer alteração no broker:

- `simulatorRawDisabled=true`;
- `preventionRawDisabled=true`;
- `noRunningSimulatorExecutions=true`;
- todas as réplicas Prevention usam a imagem esperada;
- revisões/ReplicaSets antigos foram revistos;
- nenhuma campanha controlled-validation está em execução.

### 14.2 Credenciais Management

Usar uma conta de monitorização/admin operacional temporária aprovada. Nunca
escrever a password em argumentos ou evidence:

```powershell
$env:RABBITMQ_MANAGEMENT_USERNAME = '<user>'
$env:RABBITMQ_MANAGEMENT_PASSWORD = '<password>'
```

A URI deve usar HTTPS e a CA privada:

```powershell
$management = 'https://rabbitmq.staging.natureprotector.internal:15671'
$ca = '.\artifacts\approved-inputs\rabbitmq-ca.crt'
```

HTTP só é aceite localmente com `-AllowInsecureHttp` explícito.

### 14.3 Inventário e plano do broker

```powershell
pwsh -NoProfile -File `
  .\scripts\operations\Invoke-RabbitMqRawQueueMigration.ps1 `
  -Action Inventory `
  -ManagementBaseUri $management `
  -CertificateAuthorityPath $ca

pwsh -NoProfile -File `
  .\scripts\operations\Invoke-RabbitMqRawQueueMigration.ps1 `
  -Action Plan `
  -ManagementBaseUri $management `
  -CertificateAuthorityPath $ca
```

Preservar os diretórios timestamped em
`artifacts/operational-audit/rabbitmq-health-phase3f/`.

### 14.4 Proteção bounded da raw

Os valores seguintes são exemplos de sintaxe, não valores universais. Devem ser
substituídos por números aprovados a partir de taxa, tamanho médio e janela de
retenção do ambiente:

```powershell
$ttlMs = <APPROVED_TTL_MILLISECONDS>
$maxBytes = <APPROVED_MAX_LENGTH_BYTES>
$confirmation = 'PROTECT_RAW:/:np.observability.raw'

pwsh -NoProfile -File `
  .\scripts\operations\Invoke-RabbitMqRawQueueMigration.ps1 `
  -Action Protect `
  -ManagementBaseUri $management `
  -CertificateAuthorityPath $ca `
  -MessageTtlMilliseconds $ttlMs `
  -MaxLengthBytes $maxBytes `
  -Apply `
  -Confirmation $confirmation
```

A policy produzida usa:

```text
pattern = ^np\.observability\.raw$
message-ttl > 0
max-length-bytes > 0
overflow = drop-head
priority = 90
```

### 14.5 Rollout declarativo da policy primary

O manifesto Phase 3F cria apenas:

```text
natureprotector-primary-work-queue
pattern = ^np\.ingestion\.readings$
overflow = reject-publish
```

Esperar o recurso:

```powershell
kubectl wait `
  --namespace natureprotector-staging `
  --for=condition=Ready `
  policy/natureprotector-primary-work-queue-policy `
  --timeout=5m
```

### 14.6 Retirar a policy larga antiga

Só depois de a policy primary exata e a proteção raw estarem observadas:

```powershell
$confirmation = 'RETIRE_LEGACY_POLICY:/:natureprotector-quorum'

pwsh -NoProfile -File `
  .\scripts\operations\Invoke-RabbitMqRawQueueMigration.ps1 `
  -Action RetireLegacyPolicy `
  -ManagementBaseUri $management `
  -CertificateAuthorityPath $ca `
  -Apply `
  -Confirmation $confirmation
```

### 14.7 Unbind explícito

```powershell
$confirmation = 'UNBIND_RAW:/:np.events:np.observability.raw:simulation.reading.produced'

pwsh -NoProfile -File `
  .\scripts\operations\Invoke-RabbitMqRawQueueMigration.ps1 `
  -Action Unbind `
  -ManagementBaseUri $management `
  -CertificateAuthorityPath $ca `
  -Apply `
  -Confirmation $confirmation
```

O script não executa purge nem delete da queue.

### 14.8 Verify

```powershell
pwsh -NoProfile -File `
  .\scripts\operations\Invoke-RabbitMqRawQueueMigration.ps1 `
  -Action Verify `
  -ManagementBaseUri $management `
  -CertificateAuthorityPath $ca
```

Marker esperado:

```text
PHASE3F_RAW_DISABLED_AND_UNBOUND
```

### 14.9 Rollback limitado

O rollback só restaura a binding se a queue ainda existir e continuar protegida
por TTL, cap e `drop-head`:

```powershell
$confirmation = 'ROLLBACK_RAW:/:np.events:np.observability.raw:simulation.reading.produced'

pwsh -NoProfile -File `
  .\scripts\operations\Invoke-RabbitMqRawQueueMigration.ps1 `
  -Action Rollback `
  -ManagementBaseUri $management `
  -CertificateAuthorityPath $ca `
  -Apply `
  -Confirmation $confirmation
```

O rollback não recria queues, não restaura a policy larga e não remove a policy
bounded.

## 15. Prova Phase 3G — entrega parcial e idempotência

Antes de declarar a remediação RabbitMQ concluída, executar:

```powershell
pwsh -NoProfile -File `
  .\scripts\audit\Invoke-RabbitMqHealthPhase3GValidation.ps1 `
  -IncludeDockerIntegration
```

A prova cria apenas vhosts e bases temporárias locais. Deve produzir:

```text
PHASE3G_TYPED_PUBLISH_OUTCOMES_AND_PROCESS_EXIT_PROVED
PHASE3G_PARTIAL_DELIVERY_IDEMPOTENCY_PROVED
PHASE3G_PUBLISHED_RUNTIME_PARTIAL_DELIVERY_PROVED
PHASE3G_VALIDATION=PASS
```

Critérios:

1. a raw isolada rejeita com `reject-publish`;
2. o publisher devolve uma exceção de outcome ambíguo;
3. a primary processa uma única vez;
4. o retry do mesmo EventId não cria segunda tentativa ou segunda projeção;
5. a run fica `Failed` com `EndedAt`;
6. o processo Simulator sai com código não zero;
7. a evidence fica em `artifacts/tests/rabbitmq-health-phase3g/`.

Esta prova não autoriza reativar `reject-publish` na raw real. A configuração é
fault injection local e isolada.
