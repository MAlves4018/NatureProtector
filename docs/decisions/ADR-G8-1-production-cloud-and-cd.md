# ADR G8.1 — Arquitetura cloud de produção e Continuous Delivery

## Estado

**Aceite como arquitetura-alvo estática; não provisionada.**

```text
G8_1_PRODUCTION_ARCHITECTURE_AND_CD_IMPLEMENTED_STATICALLY
CLOUD_NOT_PROVISIONED
PRODUCTION_NO_GO
production_authorized=false
production_deployed=false
```

## Contexto

As fases G3.1–G8 construíram progressivamente containers, IaC, identidade, cadeia distribuída, hardening, governação e qualificação. Porém, não existiu execução cloud e o antigo G8 concentrava-se na evidence sem fechar integralmente o comportamento de uma plataforma pública: edge, rate limiting, quotas, capacidade, scaling, supply chain e entrega progressiva.

O owner decidiu:

- permitir a reutilização da billing account académica exclusivamente como fonte de faturação, mantendo todos os workloads em projetos NatureProtector novos e isolados;
- proibir a colocação de workloads NatureProtector no projeto `cn2526-t4-g04` ou a reutilização de recursos runtime da disciplina CN;
- criar projetos novos apenas depois do G10 e da integração pelo Codex;
- preservar a arquitetura production-ready como baseline normativa e usar um perfil de qualificação efémero, explicitamente não produtivo, para a primeira prova com orçamento limitado;
- manter o ambiente final aproximadamente uma semana;
- exigir production readiness real e Continuous Delivery completo.

## Decisão

### Hierarquia

A arquitetura canónica usa três projetos novos:

1. **platform** — Artifact Registry, Terraform state, WIF, Cloud Deploy, release manifests e evidence;
2. **staging** — ambiente representativo, verificações, recovery drills e qualificação;
3. **production** — tráfego final, canary, SLOs, rollback e operação.

Não existe um modo alternativo single-project. Os IDs reais só são introduzidos depois do G10.

### Runtime

- frontend e Backoffice API: Cloud Run atrás de Global External Application Load Balancer;
- edge: Cloud Armor, TLS gerido, serverless NEGs e rate limiting;
- Simulator: Cloud Run Job finito;
- Prevention: Deployment no GKE Autopilot regional;
- broker: RabbitMQ Cluster Operator, três nós e quorum queues;
- PostgreSQL: Cloud SQL regional HA, private IP, TLS, backups e PITR;
- observabilidade: OpenTelemetry para Google Cloud Observability;
- InfluxDB não é promovido automaticamente para produção sem ADR funcional própria.

### Entrega

- GitHub Actions executa CI, build uma vez, SBOM, provenance, scan, assinatura e release manifest;
- Workload Identity Federation elimina service-account keys;
- Cloud Deploy promove os mesmos digests para staging e produção;
- produção exige GitHub Environment e target approval;
- canary usa 5%, 25%, 50% e stable, com verificação por fase;
- automações Cloud Deploy só avançam fases concluídas com sucesso;
- migrations são expand/contract e anteriores ao rollout;
- bootstrap é one-shot e executado duas vezes em staging para comprovar idempotência;
- rollback aplicacional não executa down migration automática;
- a primeira criação do edge é bifásica e fail-closed: serviços primeiro, edge depois, e nova verificação idempotente da mesma release pelo hostname protegido.

### Segurança e disponibilidade

- ingress Cloud Run limitado a load balancing interno;
- Cloud Armor no edge e rate limiting ASP.NET por operação;
- quotas funcionais e limites de capacidade são obrigatórios;
- GKE privado, WIF, Binary Authorization, Pod Security e NetworkPolicy default-deny;
- secrets permanecem no Secret Manager; payloads nunca entram em Terraform, Git ou evidence;
- production authorization continua fora do G8.1.

## Consequências

### Positivas

- separação clara entre build, staging e produção;
- promoção do mesmo artefacto, sem rebuild por ambiente;
- menor superfície pública direta;
- fail-closed perante gates, assinaturas, scans ou verificações falhadas;
- rollback e teardown ficam auditáveis;
- a semana runtime pode provar deployment, soak, drills e reconstrução.

### Custos e limitações

- três projetos, GKE regional, Cloud SQL HA e Load Balancer aumentam custo e complexidade;
- uma semana não prova estabilidade sazonal ou operação prolongada;
- valores de scaling e rate limit são candidatos até load testing;
- G8.2 ainda terá de corrigir a cadeia de evidence e autorização do antigo G8;
- G9 terá de convergir toda a linha experimental com o repositório real;
- G10 prepara a integração Codex; só depois se criam projetos e se executa cloud.

## Alternativas rejeitadas

- reutilizar o projeto CN ou recursos runtime CN; a billing account pode ser associada aos novos projetos apenas como decisão administrativa de faturação;
- projeto único para simplificar custos;
- expor URLs `run.app` diretamente;
- usar apenas limiter in-memory como defesa global;
- rebuild de imagens por ambiente;
- deployment direto por scripts sem Cloud Deploy;
- rollback automático de migrations destrutivas;
- declarar produção apenas com validação estática.

### Prevention rollout strategy

The API and frontend retain verified Cloud Deploy canaries because HTTP traffic can be partitioned by revision. Prevention does not use a percentage canary: multiple consumer versions attached to the same RabbitMQ queue cannot receive a deterministic percentage of events, and concurrent schema/behavior versions would weaken replay and idempotency reasoning. Prevention therefore uses an approved, verified rolling deployment with a PodDisruptionBudget, topology spread, KEDA bounds, compatibility checks and Cloud Deploy rollback.
