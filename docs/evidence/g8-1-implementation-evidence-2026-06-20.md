# G8.1 — Evidence de implementação estática — 2026-06-20

## Scope

Implementação sobre o ZIP G8 fornecido. Não foi usado Git mutável, não foram criados projetos, não houve `terraform apply`, deployment, migração remota ou acesso a secrets.

## Implementado

- API rate limiting configurável e testes;
- arquitetura multi-project exclusiva e não CN;
- roots Terraform state-bootstrap, platform e environment com backend GCS;
- Load Balancer, Cloud Armor e ingress restringido;
- GKE Autopilot, RabbitMQ quorum e scaling Prevention por KEDA sobre queue depth/CPU;
- Cloud SQL regional HA/PITR;
- WIF específico por workflow, com provider condition exata e binding IAM por atributo URI-safe;
- Cloud Deploy staging/production, approvals, canary API/frontend, rollout verificado Prevention e bootstrap idempotente do edge;
- workflows policy, release, staging, production e teardown;
- release manifest por digest para onze imagens;
- scripts de build, runtime jobs, staging, promoção, smoke protegido, owner gate e teardown;
- políticas machine-readable;
- ADR, implementação, segurança e runbooks.

## Limites

Neste ambiente não estavam disponíveis `.NET`, Terraform CLI, PowerShell, Docker, gcloud, kubectl ou GCP. Por isso:

- testes .NET não executados;
- Terraform apenas parsed por `python-hcl2` antes do gate final;
- scripts PowerShell apenas analisados estaticamente;
- containers não construídos;
- nenhuma integração externa provada;
- todos os valores de rate limiting e scaling continuam candidatos;
- não foi possível provar a sequência bifásica do primeiro edge, apenas validar os seus guardrails e estados fail-closed.

## Claim permitido

```text
G8_1_PRODUCTION_ARCHITECTURE_AND_CD_IMPLEMENTED_STATICALLY
CLOUD_NOT_PROVISIONED
PRODUCTION_NO_GO
```

## Próximo gate

G8.2 deve corrigir a integridade da qualificação/evidence antiga. G9 converge as fases sobre a baseline real. G10 produz o handoff Codex. A prova cloud só ocorre depois da integração e criação de projetos novos.


## Validação final reproduzida

| Gate | Resultado |
|---|---:|
| Policy/estrutura G8.1 | 265/265 PASS |
| Regressão G8 | 1 622/1 622 PASS |
| Regressão G7 | 360/360 PASS |
| Manifesto de release | PASS — 11 imagens |
| Python cloud | compile PASS |
| Shell cloud | syntax PASS |
| TypeScript | PASS |
| Biome | 60 ficheiros PASS |
| Vitest | 47/47 PASS |
| Vite | PASS — 4 180 módulos |
| Audit-policy tests | 10/10 PASS |
| npm HIGH/CRITICAL | 0 |
| Contaminação de packaging | 0 findings |

A validação estática prova coerência estrutural e guardrails. Não substitui `terraform validate`, compilação/testes .NET, build de imagens nem execução GCP.
