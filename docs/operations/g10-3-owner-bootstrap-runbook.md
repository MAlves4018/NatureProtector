# Runbook G10.3 — Projetos vazios, budgets, inventário e foundation plan

## 1. Estado inicial obrigatório

- usar o repositório remediado mais recente;
- `gh` e `gcloud` autenticados na conta correta;
- input G10.2 real fora do repositório;
- IDs GitHub numéricos resolvidos;
- IDs dos três projetos aprovados;
- crédito atual revisto no momento da execução;
- nenhum data plane autorizado.

## 2. Preflight completo

```powershell
pwsh scripts/cloud/Invoke-G102ExecutablePreflight.ps1 `
  -InputPath $env:TEMP/natureprotector-g10-2-bootstrap-input.json `
  -EvidenceDirectory artifacts/g10-2-preflight `
  -RequireAllTools
```

Não continuar se o resultado não for `PASS`.

## 3. Autorizar explicitamente os projetos vazios

No input G10.2 real, definir apenas:

```json
"create_projects": true,
"link_billing": true
```

Todas as restantes flags continuam a `false`.

Simular:

```powershell
pwsh scripts/cloud/Invoke-G102ProjectBootstrap.ps1 `
  -InputPath $env:TEMP/natureprotector-g10-2-bootstrap-input.json `
  -Confirmation "CREATE_EMPTY_NATUREPROTECTOR_PROJECTS_AND_LINK_APPROVED_BILLING" `
  -EvidenceDirectory artifacts/g10-2-bootstrap `
  -Execute `
  -WhatIf
```

Executar apenas depois de rever o `project-bootstrap-summary.json`, retirando
`-WhatIf`.

## 4. Inventário pós-bootstrap

```powershell
pwsh scripts/cloud/Get-G103CloudInventory.ps1 `
  -InputPath $env:TEMP/natureprotector-g10-2-bootstrap-input.json `
  -EvidenceDirectory artifacts/g10-3-inventory
```

O inventário deve provar:

- três projetos `ACTIVE`;
- billing ligada à conta aprovada;
- ausência de Compute, GKE, Cloud Run, Cloud SQL e outros recursos inesperados;
- APIs de runtime ainda não ativadas intencionalmente.

Comandos bloqueados por APIs não ativas são `PARTIAL`, não equivalem a recursos
existentes. Confirmar no Cloud Console quando necessário.

## 5. Preparar budgets

Copiar o exemplo para fora do repositório e rever valores/currency:

```powershell
Copy-Item `
  infra/gcp/contracts/g10-3-budget-input.example.json `
  $env:TEMP/natureprotector-g10-3-budget-input.json
notepad $env:TEMP/natureprotector-g10-3-budget-input.json
```

Os valores do exemplo são candidatos, não uma autorização. A moeda tem de ser a
moeda real da billing account.

Validar:

```powershell
python scripts/cloud/Test-G103BudgetInput.py `
  --input $env:TEMP/natureprotector-g10-3-budget-input.json
```

Para execução, definir `create_budget_alerts=true` e simular:

```powershell
pwsh scripts/cloud/Invoke-G103BudgetBootstrap.ps1 `
  -InputPath $env:TEMP/natureprotector-g10-3-budget-input.json `
  -Confirmation "CREATE_NATUREPROTECTOR_BUDGET_ALERTS_ONLY" `
  -EvidenceDirectory artifacts/g10-3-budgets `
  -Execute `
  -WhatIf
```

Retirar `-WhatIf` apenas após rever todos os budgets. Budgets enviam alertas; não
param serviços nem impõem hard cap.

## 6. Gerar plano de foundation sem criar recursos

```powershell
python scripts/cloud/New-G103FoundationPlan.py `
  --input $env:TEMP/natureprotector-g10-2-bootstrap-input.json `
  --output-directory artifacts/g10-3-foundation-plan
```

Os ficheiros gerados mantêm:

```text
create_state_foundation=false
create_delivery_control_plane=false
create_delivery_pipelines=false
```

Executar apenas `fmt`, `init -backend=false`, `validate` e `plan` read-only com
essas flags. A criação do state bucket exige fase e autorização posteriores.

## 7. Gate de saída

```text
PROJECTS_CREATED_AND_BILLING_LINKED
PROJECT_NUMBERS_RECORDED
EMPTY_PROJECT_INVENTORY_REVIEWED
BUDGET_ALERTS_PROVED
FOUNDATION_PLAN_GENERATED
STATE_FOUNDATION_NOT_CREATED
DATA_PLANE_NOT_CREATED
DEPLOYMENT_NOT_CLAIMED
```
