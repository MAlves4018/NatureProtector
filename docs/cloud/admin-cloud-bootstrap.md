# Bootstrap administrativo Google Cloud

## Objetivo

Configurar a camada de controlo do projeto de forma idempotente e auditável, antes de criar infraestrutura de staging.

## Sequência completa

### 1. Proteger a conta

- autenticação de dois fatores;
- email e telefone de recuperação;
- códigos de recuperação guardados fora do computador;
- confirmação do estado Free Trial;
- não fazer upgrade para pay-as-you-go sem decisão explícita.

### 2. Confirmar os identificadores

- Project ID;
- Billing Account ID;
- região primária;
- conta Google ativa;
- estado `billingEnabled`.

### 3. Criar budget antes de runtime

O script cria, se ainda não existir, um budget mensal filtrado pelo projeto, com alertas a 25%, 50%, 75%, 90%, 100% e previsão de 100%.

O budget é um alerta, não um bloqueio automático de gastos.

### 4. Guardar configuração fora do código

```powershell
.\scripts\cloud\Set-CloudEnvironment.ps1 `
  -ProjectId "natureprotector-500518" `
  -BillingAccountId "<BILLING_ACCOUNT_ID>" `
  -Region "europe-southwest1" `
  -ConfigurationName "natureprotector-personal" `
  -BudgetAmount "20EUR" `
  -Scope Process
```

### 5. Preparar autenticação local

```powershell
.\scripts\cloud\Setup-CloudDeveloper.ps1 `
  -Account "<EMAIL_OWNER>"
```

### 6. Executar dry run administrativo

```powershell
.\scripts\cloud\Initialize-CloudProject.ps1
```

O dry run:

- descreve o projeto;
- compara a ligação de billing;
- calcula APIs em falta;
- verifica se o budget já existe;
- cria evidence;
- não altera a cloud.

### 7. Aplicar alterações permitidas

Quando a billing já está correta:

```powershell
.\scripts\cloud\Initialize-CloudProject.ps1 -Apply
```

Quando é preciso alterar a billing:

```powershell
.\scripts\cloud\Initialize-CloudProject.ps1 `
  -Apply `
  -AllowBillingLink
```

A opção adicional evita que um simples `-Apply` mude acidentalmente a conta de faturação.

### 8. Validar

```powershell
.\scripts\cloud\Test-CloudSetup.ps1 `
  -RequireTerraform `
  -RequireDocker `
  -RequireKubectl
```

### 9. Reconciliar com o repositório real

Antes de acrescentar mais automação, inspecionar:

- módulos Terraform existentes;
- backend/state;
- projetos platform/staging/production;
- Artifact Registry existente;
- service accounts;
- Workload Identity Federation;
- GitHub Actions;
- APIs realmente exigidas;
- manifests Kustomize/Kubernetes;
- scripts e evidence já existentes.

### 10. Implementar IAM mínimo e CI/CD

A fase seguinte deve:

- separar owner, developer e CI;
- usar least privilege;
- usar Workload Identity Federation para GitHub Actions;
- não criar chaves JSON permanentes;
- limitar a identidade federada ao repositório, branch ou environment autorizado;
- manter billing fora dos workflows normais de deployment.

### 11. Criar staging só depois

O setup administrativo não cria:

- GKE;
- Cloud SQL;
- load balancers;
- discos;
- IPs;
- Cloud NAT;
- runtime da aplicação.

Esses recursos devem ser introduzidos por Terraform, primeiro através de `plan`, com teardown preparado antes de `apply`.
