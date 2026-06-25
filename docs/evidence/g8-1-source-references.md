# G8.1 — Fontes primárias

Consultadas em 20 de junho de 2026. As decisões são implementações do NatureProtector; as fontes suportam apenas capacidades e constraints dos produtos.

- Google Cloud Architecture Framework — system design, reliability, security and operational excellence.
- Google Cloud Resource Manager — resource hierarchy and project boundaries.
- Cloud Run — autoscaling, concurrency, ingress and health checks.
- Cloud Load Balancing — serverless NEGs and Cloud Run backends.
- Cloud Armor — rate limiting, WAF and adaptive protection.
- GKE — Autopilot, private clusters, Workload Identity and security posture.
- KEDA — `ScaledObject`, RabbitMQ scaler, TriggerAuthentication, fallback e HPA behavior gerado.
- Cloud SQL for PostgreSQL — regional HA, PITR, connection management and managed pooling.
- Cloud Deploy — Cloud Run/GKE targets, canary strategies, approvals, verification, automation and rollback.
- Workload Identity Federation — GitHub OIDC without service-account keys.
- Binary Authorization — attestations and deployment enforcement.
- GitHub Actions — OIDC, environments, protected deployments and concurrency.
- ASP.NET Core — partitioned rate limiting and health checks.
- RabbitMQ — quorum queues, cluster sizing and delivery limits.
- DORA — Continuous Delivery, database change management and small batches.
- OWASP API Security Top 10 2023 — unrestricted resource consumption.

URLs can be revalidated through the official product documentation before provisioning. No third-party tutorial is normative for the implementation.


## Toolchain verificada em 20 de junho de 2026

- Terraform CLI `1.15.6`: versão usada na qualificação offline G11.3; os roots G8.1 aceitam a patchline `1.15.x` através de `~> 1.15.5`.
- Google provider `7.36.0`: versão `latest` no Terraform Registry nesta data.
- Random provider `3.9.0`: versão `latest`; a variante efémera de `random_password` é usada com argumentos write-only para evitar payloads em plan/state.
