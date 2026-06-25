# Runbook G8.1 — Continuous Delivery e rollouts

## Pré-condições pós-G10

- três projetos NatureProtector novos e isolados (`platform`, `staging`, `production`);
- billing account aprovada; pode ser a conta académica autorizada pelo owner, mas nenhum workload ou recurso runtime pode ser criado no projeto CN;
- environments GitHub `staging`, `production` e `production-operations`;
- reviewers configurados em produção;
- WIF e service accounts criados pelo root platform;
- roots environment aplicados com `create_data_plane=true`;
- versões TLS/CA criadas pelo owner; credenciais de aplicação materializadas por write-only arguments quando explicitamente ativadas;
- Cloud Run jobs de migrations/bootstrap preparados;
- DNS e certificados disponíveis;
- owner gate local integral aprovado.


## Perfil de primeira qualificação

A primeira implantação comprovável usa o projeto `staging` com o perfil de
qualificação efémero. Este perfil mantém os mesmos artefactos, contratos, TLS,
PostgreSQL, RabbitMQ e OTLP, mas permite Cloud Run `minScale=0`, um único nó
RabbitMQ, uma réplica Prevention e Cloud SQL zonal sem PITR. Não constitui
evidence de alta disponibilidade nem autorização de produção.

Antes de `create_data_plane=true` são obrigatórios: estimativa de custo, budget,
lista de recursos esperados, janela de execução e teardown ensaiado. A promoção
para produção continua a exigir o perfil production-ready e os gates existentes.

## Release

1. merge pequeno para `master`;
2. Engineering Foundations, Security e G8.1 policy no mesmo SHA;
3. workflow de release constrói as onze imagens;
4. verificar manifest, scans, SBOM, provenance e Cosign;
5. nunca editar o artifact depois da attestation.

## Staging

### Primeiro bootstrap do edge

1. executar manualmente o workflow com `deployment_mode=services-only-bootstrap`;
2. escrever `BOOTSTRAP_SERVICES_BEFORE_EDGE`;
3. migrations, bootstrap, jobs e rollouts são executados;
4. a summary termina com `staging_verified=false` e `edge_bootstrap_pending=true`;
5. aplicar o edge Terraform agora que API e frontend existem;
6. repetir a mesma release com `deployment_mode=verified`.

### Execução normal

1. workflow automático ou manual recebe o run ID da release;
2. valida path, conclusion e SHA do workflow de origem;
3. executa migration expand;
4. executa bootstrap duas vezes;
5. cria ou reutiliza idempotentemente três releases Cloud Deploy;
6. aguarda a conclusão dos rollouts;
7. executa o smoke funcional pelo hostname HTTPS protegido; URLs `run.app` são rejeitadas;
8. arquiva evidence e checksums com `staging_verified=true`.

Falha em migration, bootstrap, rollout ou smoke bloqueia promoção.

## Produção

1. selecionar o `staging_run_id` aprovado;
2. confirmar que o manifesto é byte-for-byte idêntico;
3. iniciar `G8.1 promote verified release to production`;
4. GitHub Environment exige reviewer distinto quando disponível;
5. escrever `PROMOTE_VERIFIED_RELEASE_TO_PRODUCTION`;
6. Cloud Deploy cria rollout que exige aprovação;
7. verificar o rollout e aprová-lo;
8. cada fase canary é verificada;
9. automação avança apenas fases bem-sucedidas após 300 s;
10. falha interrompe a progressão e inicia rollback operacional.

## Primeira implantação

Cloud Deploy não tem uma revisão estável anterior e o edge não pode criar serverless NEGs antes dos serviços Cloud Run existirem. O bootstrap é por isso deliberadamente bifásico:

1. `services-only-bootstrap` cria os serviços e rollouts, mas não executa smoke nem declara o ambiente verificado;
2. Terraform ativa Load Balancer, Cloud Armor, certificado e serverless NEGs;
3. a mesma release é repetida idempotentemente em modo `verified` e executa o smoke pelo edge;
4. em produção continua a ser exigida a confirmação `I_ACCEPT_FIRST_RELEASE_HAS_NO_CANARY_BASELINE`;
5. uma release posterior é necessária para provar canary real contra uma baseline anterior.

O bootstrap técnico nunca pode ser usado como staging evidence para produção, porque `staging_verified=false` é rejeitado pelo script de promoção.

## Rollback

- aplicação: promover o último digest verificado ou usar rollback Cloud Deploy;
- base de dados: manter schema expand compatível; não executar down migration automática;
- filas: preservar EventId e idempotência;
- canary falhado: não avançar fase;
- estado do tráfego, release, rollout e reason devem entrar na evidence.

## Concurrency

- uma release por SHA;
- um staging deployment ativo;
- um production rollout ativo;
- teardown nunca corre em paralelo com rollout;
- jobs migrations/bootstrap são serializados.

## Evidence mínima

- release manifest e attestation;
- qualidade same-SHA;
- imagens e digests;
- scans e assinaturas;
- migration/bootstrap executions;
- Cloud Deploy release e rollouts;
- resultados de verify;
- SLO snapshot antes/depois;
- rollback, quando aplicável;
- checksums.

### Cluster dependency gate

Before any GKE workload rollout, the environment workflow obtains credentials through the GKE DNS endpoint and executes `Install-G81ClusterDependencies.ps1`. The gate is idempotent and fail-closed: an unpinned tag, missing release asset, absent GitHub-published SHA-256 digest, digest mismatch or failed operator rollout stops deployment. The exact cert-manager, RabbitMQ Cluster Operator, RabbitMQ Messaging Topology Operator and KEDA releases are recorded under `cluster-dependencies/cluster-dependencies.json`.

The workflow identity receives `roles/container.admin` only in its own environment project because installing CRDs and cluster-scoped RBAC cannot be performed with namespace-only deployment permissions. Trust remains restricted to the exact staging or production workflow reference by the platform WIF provider. Runtime identities do not receive this role.

### Why Prevention is not a percentage canary

`natureprotector-prevention` consumes a shared quorum queue. Cloud Deploy service-network canaries are appropriate for request-routed workloads, not for AMQP consumers where the broker distributes deliveries across all active consumers. Production promotes Prevention through a verified rolling rollout; API and frontend continue through 5% → 25% → 50% → 100% verified canary phases. A Prevention verification failure halts the rollout and requires Cloud Deploy rollback to the last successful release.
