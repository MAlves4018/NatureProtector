# Runbook G8.1 — Continuous Delivery e rollouts

## Pré-condições pós-G10

- três projetos NatureProtector novos e isolados (`platform`, `staging`, `production`);
- billing account aprovada; pode ser a conta académica autorizada pelo owner, mas nenhum workload ou recurso runtime pode ser criado no projeto CN;
- environments GitHub `staging`, `production` e `production-operations`;
- reviewers configurados em produção;
- WIF e service accounts criados pelo root platform;
- roots environment aplicados com `create_data_plane=true`;
- versões TLS/CA criadas pelo owner; credenciais de aplicação materializadas por write-only arguments quando explicitamente ativadas;
- GitHub Environment `staging` aponta para versões Secret Manager explícitas e `ENABLED` para Cloud SQL CA, RabbitMQ CA, RabbitMQ TLS certificate e RabbitMQ TLS private key; `latest` não é aceite no preflight;
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
3. valida que os secrets owner-managed existem, que as versões configuradas existem e estão `ENABLED`, e que as service accounts runtime têm `roles/secretmanager.secretAccessor`;
4. executa migration expand;
5. executa bootstrap duas vezes;
6. garante idempotentemente o namespace `natureprotector-staging` e o support RBAC/NetworkPolicy do verifier antes da release Prevention;
7. antes de promover o rollout Prevention, executa `Test-G81PreventionPreRolloutQualification.ps1`;
8. cria ou reutiliza idempotentemente três releases Cloud Deploy;
9. aguarda a conclusão dos rollouts;
10. os verifies Cloud Deploy dos serviços Cloud Run confirmam apenas que o rollout publicou metadata `run.app`, porque o ingress direto está restrito a `internal-and-cloud-load-balancing`;
11. executa o smoke funcional pelo hostname HTTPS protegido; URLs `run.app` são rejeitadas;
12. arquiva evidence e checksums com `staging_verified=true`.

Falha em migration, bootstrap, rollout ou smoke bloqueia promoção.

No rollout GKE, confirmar que `prevention-runtime` permite egress para o
`cloud_sql_private_cidr` real e que os pods `Prevention.Host` recebem
`RabbitMq__TlsEnabled=true`, `RabbitMq__TlsServerName` e
`RabbitMq__TlsCertificateAuthorityPath=/var/run/secrets/rabbitmq/ca.crt`.
O gate pre-rollout da Prevention falha fechado se os deploy parameters do alvo
`np-gke-staging` tiverem `cloud_sql_private_ip`,
`cloud_sql_private_cidr` ou `rabbitmq_tls_server_name` diferentes dos valores
runtime, se o render continuar com placeholders, se o server-side dry-run for
rejeitado, ou se dependencias live como SecretProvider-synced secrets, SAN do
certificado RabbitMQ ou IP privado Cloud SQL nao baterem com o contrato.
Se o `ScaledObject` KEDA reportar `no queue 'np.ingestion.readings'`, primeiro
validar que o pod Prevention ficou Ready; a fila é declarada pelo worker no
arranque e a ausência da fila pode ser sintoma de falha de ligação anterior.
Se o rollout terminal falhar com `ProgressDeadlineExceeded` ou readiness 503,
classificar como `PREVENTION_ROLLOUT_FAILURE_CLASS=READINESS_OR_LIVENESS` ate
prova em contrario e consultar `*-rollout-failure-diagnostics/` para rollout,
job-runs, events, describe e logs Kubernetes. Erros de TLS KEDA do tipo
`certificate is valid for ... not ...svc.cluster.local` indicam SAN RabbitMQ
incompativel com o host AMQPS usado pelo `ScaledObject`; nao usar `unsafeSsl`
como correcao normal.
Se KEDA estiver `Ready=True` mas Prevention continuar com readiness 503 e logs
`RabbitMQ is temporarily unavailable` / `RemoteCertificateValidationCallback`,
comparar a hora de arranque do pod com a rotação dos Secrets TLS. O worker
recria o `ConnectionFactory` em cada retry para reler a CA privada montada; se
uma imagem anterior estiver ativa, reiniciar a release e verificar que o pod
carregou a CA atual.

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
- resultados de verify e smoke HTTPS pelo edge protegido;
- SLO snapshot antes/depois;
- rollback, quando aplicável;
- checksums.

### Cluster dependency gate

Before any GKE workload rollout, the environment workflow obtains credentials through the GKE DNS endpoint and executes `Install-G81ClusterDependencies.ps1`. The gate is idempotent and fail-closed: an unpinned tag, missing release asset, absent GitHub-published SHA-256 digest, digest mismatch or failed operator rollout stops deployment. The exact cert-manager, RabbitMQ Cluster Operator, RabbitMQ Messaging Topology Operator and KEDA releases are recorded under `cluster-dependencies/cluster-dependencies.json`.

On GKE Autopilot, `Install-G81ClusterDependencies.ps1` first checks whether all operator rollouts from `operator-lock.json` are already ready. If they are, it records `OPERATOR_FOUNDATION_ALREADY_READY` and does not reinstall the operators. If remediation is needed, it delegates to `install-g81-cluster-dependencies-autopilot.sh`, which resolves Python portably, validates the pinned PyYAML dependency, mirrors the pinned operator images into Artifact Registry as Linux/amd64 digest references, and then applies patched manifests. Runtime pods therefore do not depend on pulling controller images from external registries such as `quay.io`. The bootstrap does not mutate Artifact Registry IAM; it fails closed unless the repository policy already grants `roles/artifactregistry.reader` to the real GKE node service account managed by Terraform. Manifest mutations are scoped to digest image rewrites, the cert-manager leader-election namespace correction, and explicit low requests for the KEDA bootstrap deployments only. Bootstrap failures are classified as `IMAGE_PULL`, `RESOURCE_REQUEST_OR_ADMISSION`, `CONTAINER_CRASH`, `QUOTA` or `UNKNOWN` with diagnostics captured in the evidence directory.

The workflow identity receives `roles/container.admin` only in its own environment project because installing CRDs and cluster-scoped RBAC cannot be performed with namespace-only deployment permissions. Trust remains restricted to the exact staging or production workflow reference by the platform WIF provider. Runtime identities do not receive this role.

RabbitMQ topology is reconciled through the default-user `connectionSecret`
against the internal management HTTP port 15672. The application AMQP path
remains TLS-only on 5671 with private CA validation; only the topology operator
namespace is allowed to reach 15672 by NetworkPolicy.

### Why Prevention is not a percentage canary

`natureprotector-prevention` consumes a shared quorum queue. Cloud Deploy service-network canaries are appropriate for request-routed workloads, not for AMQP consumers where the broker distributes deliveries across all active consumers. Production promotes Prevention through a verified rolling rollout; API and frontend continue through 5% → 25% → 50% → 100% verified canary phases. A Prevention verification failure halts the rollout and requires Cloud Deploy rollback to the last successful release.
