---
id: NP-CURRENT-ROLES
status: CURRENT
owner: Miguel Alves
audience: engineering, report, presentation
source_of_truth: OperationCapabilities.cs, OperationRoleCatalog, App.tsx and pageRegistry.ts
last_verified_against: NatureProtector repository snapshot 2026-07-22
last_verified_at: 2026-07-22
review_triggers: role, capability, authorization or route changes
---

# Roles, capabilities e jornadas UI

## Modelo de autoridade

O backend transforma roles em capabilities e aplica policies nos controllers/serviços. O frontend consulta `GET /api/users-roles/me/capabilities` para filtrar navegação e affordances. A ocultação de um botão ou rota nunca substitui a autorização da API.

Matriz integral gerada: [../reference/generated/role-capability-matrix.csv](../reference/generated/role-capability-matrix.csv).

## Roles

| Role | Finalidade principal | Limites importantes |
| --- | --- | --- |
| `Pipeline` | Ler risco, pipeline, qualidade e evidence | Não executa simulações nem operações cloud |
| `Sim` | Consultar cenários, executar simulações e ler evidence relacionada | Sem qualidade full, deployments ou administração |
| `QA` | Executar qualidade/evidence e simulações de validação | Sem cloud/deployment de produção |
| `Operations` | Ler pipeline/evidence e operar staging/deployment | Sem promoção automática a produção ou destroy |
| `ReleaseApprover` | Rever aprovações, produção, rollback e destroy | Não administra utilizadores |
| `Admin` | Administração da aplicação, runtime, utilizadores/roles e P3 read | Não recebe automaticamente capabilities de produção/destroy nem `approval.review` |

Uma identidade pode acumular roles. Confirmação, aprovação e capability continuam separadas.

## Rotas atuais

| Grupo | Rota | Capability(s) |
| --- | --- | --- |
| Público | `/demo` | `demo.read` |
| Público | `/dashboard` | `area.read` |
| Público | `/context` | `data_context.read` |
| Público | `/about` | `demo.read` |
| Operação | `/overview` | `quality.read` |
| Operação | `/mission` | `quality.read` |
| Operação | `/risk` | `risk.read` |
| Simulação | `/runs` | `run.read` |
| Simulação | `/simulation` | `simulation.read` |
| Simulação | `/scenario-compare` | `run.read` |
| Simulação | `/queries` | `simulation.execute` |
| Técnica | `/pipeline` | `pipeline.read` |
| Técnica | `/qa` | `qa.read` |
| Técnica | `/evidence` | `evidence.read` |
| Release | `/deployments` | `deployment.read` |
| Release | `/deployment-health` | `deployment.read` |
| Release | `/cloud` | `cloud.read` |
| Release | `/approvals` | `approval.review` |
| Administração | `/users` | `users.manage` e `roles.manage` |
| Administração | `/admin` | `admin.read` |
| Administração | `/p3` | `p3.read` |

A tabela gerada completa, incluindo aliases e superfícies retiradas, está em [../reference/generated/ui-route-capability-matrix.csv](../reference/generated/ui-route-capability-matrix.csv).

## Aliases e inconsistências conhecidas

- `/db-queries` redireciona para `/queries`.
- `/qa-tests` apresenta uma superfície explicitamente retirada e não gera resultados.
- `/quality` está montada e registada no `UI_PAGE_REGISTRY`, exige `quality.read` e é apresentada na navegação técnica dos perfis autorizados.
- `/dev/runtime` e `/ui-v2` são referências históricas e não superfícies atuais suportadas.

## Jornadas mínimas por perfil

| Perfil | Jornada esperada |
| --- | --- |
| Anónimo | demo → dashboard/context → login; sem navegação protegida |
| Pipeline | overview/risk → runs → pipeline → QA/evidence read |
| Sim | overview/risk → runs → simulation → comparison/queries conforme capabilities |
| QA | overview → simulation de validação → QA → evidence/compare |
| Operations | overview → pipeline/evidence → deployments/deployment-health → cloud staging |
| ReleaseApprover | evidence → approvals → operações de produção explicitamente aprovadas |
| Admin | overview → runtime/simulation → users → admin → P3 read; mutações continuam sujeitas às policies específicas |

## Separação de poderes

`Admin != production deploy/destroy`. Esta opção evita acoplar gestão de identidades à autoridade de infraestrutura. Mesmo num projeto académico com uma pessoa, a modelação conserva as decisões distintas.

## Limitações atuais

- Operações remotas dependem de providers, segredos e callbacks configurados.
- Entradas `blocked-*` no catálogo são deliberadamente indisponíveis.
- A UI pode apresentar `Not available`/`Not proved`; não deve inferir sucesso por ausência de erro.
- A matriz live por todas as roles ainda deve ser integrada na campanha final automática.
