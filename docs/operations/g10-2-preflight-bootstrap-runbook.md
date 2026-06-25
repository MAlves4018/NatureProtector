# Runbook G10.2 — Preflight e bootstrap controlado

## 1. Limites desta etapa

Esta etapa pode:

- confirmar repositório e branch;
- recolher IDs numéricos GitHub;
- validar o ambiente local;
- executar testes, Terraform validate e Kustomize build quando as ferramentas existem;
- consultar identidade, billing e projetos GCP em modo read-only;
- gerar um plano de criação;
- após autorização explícita, criar apenas projetos vazios e associar billing.

Esta etapa não pode:

- executar Terraform apply;
- criar state bucket, WIF, registry ou Cloud Deploy;
- ativar data plane, edge ou segredos;
- implantar NatureProtector;
- alegar production readiness ou deployment provado.

## 2. Preparar o input local

Copiar sem versionar:

```powershell
Copy-Item `
  infra/gcp/contracts/g10-2-bootstrap-input.example.json `
  $env:TEMP/natureprotector-g10-2-bootstrap-input.json
```

Preencher:

- `repository_id` e `repository_owner_id` obtidos pela API GitHub;
- IDs globais definitivos de platform, staging e production;
- nomes globais dos buckets;
- conta `gcloud` esperada;
- janela e responsável pelo teardown;
- crédito observado imediatamente antes da sessão;
- saldo mínimo a preservar.

O ficheiro real contém dados operacionais e não deve ser commitado.

## 3. Recolher GitHub

```powershell
$Repo = "MAlves4018/NatureProtector"
gh api "repos/$Repo" --jq `
  '{repository:.full_name,repository_id:(.id|tostring),owner_login:.owner.login,owner_id:(.owner.id|tostring),default_branch:.default_branch,visibility:.visibility}'
```

Resultado obrigatório:

```text
repository = MAlves4018/NatureProtector
default_branch = master
visibility = public
repository_id = número real
owner_id = número real
```

## 4. Validar input

```powershell
python scripts/cloud/Test-G102BootstrapInput.py `
  --input $env:TEMP/natureprotector-g10-2-bootstrap-input.json
```

O gate só passa com IDs numéricos reais, três projetos distintos, região Madrid,
billing aprovada, janela até sete dias e todas as flags de recursos a `false`.

## 5. Preflight executável e read-only

```powershell
pwsh scripts/cloud/Invoke-G102ExecutablePreflight.ps1 `
  -InputPath $env:TEMP/natureprotector-g10-2-bootstrap-input.json `
  -EvidenceDirectory artifacts/g10-2-preflight `
  -RequireAllTools
```

Ferramentas esperadas:

- .NET SDK 9.0.313;
- Node 22.16+ e npm suportado;
- Python e dependências de validação;
- Terraform 1.15.6 compatível com `~> 1.15.5`;
- Kustomize;
- `gh` autenticado;
- `gcloud` autenticado.

O script produz `preflight-summary.json`. A ausência de uma ferramenta é
`BLOCKED`, não prova de defeito do NatureProtector.

## 6. Gerar plano sem executar

```powershell
python scripts/cloud/New-G102BootstrapPlan.py `
  --input $env:TEMP/natureprotector-g10-2-bootstrap-input.json `
  --output artifacts/g10-2-preflight/bootstrap-plan.json
```

Rever os três IDs, a billing account e os comandos antes de qualquer criação.

## 7. Bootstrap opcional de projetos vazios

Só depois de o owner aprovar o plano:

```powershell
pwsh scripts/cloud/Invoke-G102ProjectBootstrap.ps1 `
  -InputPath $env:TEMP/natureprotector-g10-2-bootstrap-input.json `
  -Confirmation "CREATE_EMPTY_NATUREPROTECTOR_PROJECTS_AND_LINK_APPROVED_BILLING" `
  -Execute `
  -WhatIf
```

Primeiro usar `-WhatIf`. Retirar `-WhatIf` apenas após rever o output.

O script cria no máximo:

- projeto platform vazio;
- projeto staging vazio;
- projeto production vazio;
- ligação dos três à billing account aprovada.

Não ativa APIs e não cria recursos faturáveis de runtime.

## 8. Evidência de saída

Guardar:

- input validado sem segredos;
- metadata GitHub;
- conta `gcloud` ativa;
- billing visível;
- inventário de projetos;
- versões das ferramentas;
- testes locais;
- Terraform validate;
- Kustomize render;
- plano não executado;
- outputs de `projects describe` e `billing projects describe`, caso a criação seja autorizada.

## 9. Gate seguinte

A fase seguinte só pode começar quando:

```text
LOCAL_TESTS_PASS
TERRAFORM_VALIDATE_PASS
KUSTOMIZE_RENDER_PASS
GITHUB_NUMERIC_IDENTITY_PROVED
PROJECT_IDS_APPROVED
BILLING_LINKS_PROVED
BUDGET_ALERTS_CONFIGURED
DATA_PLANE_STILL_NOT_CREATED
```
