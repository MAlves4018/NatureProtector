# Variáveis de ambiente cloud

Os scripts não leem `.env` nem `.env.example`. Leem variáveis do processo ou do perfil do utilizador.

## Variáveis principais

| Variável | Obrigatória | Utilização |
|---|---:|---|
| `NATUREPROTECTOR_PROJECT_ID` | Sim | Projeto Google Cloud selecionado. |
| `NATUREPROTECTOR_BILLING_ACCOUNT_ID` | Para tarefas administrativas | Ligação de billing e budgets. |
| `NATUREPROTECTOR_REGION` | Não | Predefinição: `europe-southwest1`. |
| `NATUREPROTECTOR_GCLOUD_CONFIGURATION` | Não | Predefinição: `natureprotector-personal`. |
| `NATUREPROTECTOR_BUDGET_AMOUNT` | Não | Predefinição: `20EUR`. |
| `NATUREPROTECTOR_STAGING_PROJECT_ID` | Apenas teardown | Projeto isolado que pode ser destruído. |
| `NATUREPROTECTOR_EVIDENCE_DIR` | Não | Diretório de evidence local. |

## Definição apenas na sessão atual

```powershell
$env:NATUREPROTECTOR_PROJECT_ID = "natureprotector-500518"
$env:NATUREPROTECTOR_BILLING_ACCOUNT_ID = "<BILLING_ACCOUNT_ID>"
$env:NATUREPROTECTOR_REGION = "europe-southwest1"
$env:NATUREPROTECTOR_GCLOUD_CONFIGURATION = "natureprotector-personal"
$env:NATUREPROTECTOR_BUDGET_AMOUNT = "20EUR"
```

Ao fechar a PowerShell, estes valores desaparecem.

## Definição persistente para o utilizador Windows

```powershell
[Environment]::SetEnvironmentVariable(
  "NATUREPROTECTOR_PROJECT_ID",
  "natureprotector-500518",
  "User"
)

[Environment]::SetEnvironmentVariable(
  "NATUREPROTECTOR_BILLING_ACCOUNT_ID",
  "<BILLING_ACCOUNT_ID>",
  "User"
)
```

Abra uma nova PowerShell depois de usar `SetEnvironmentVariable(..., "User")`.

## Utilização recomendada

```powershell
.\scripts\cloud\Set-CloudEnvironment.ps1 `
  -ProjectId "natureprotector-500518" `
  -BillingAccountId "<BILLING_ACCOUNT_ID>" `
  -Region "europe-southwest1" `
  -Scope Process
```

Para persistir:

```powershell
.\scripts\cloud\Set-CloudEnvironment.ps1 `
  -ProjectId "natureprotector-500518" `
  -BillingAccountId "<BILLING_ACCOUNT_ID>" `
  -Region "europe-southwest1" `
  -Scope User
```

## Segurança

O Billing Account ID não funciona como senha e não permite, por si só, cobrar ou administrar recursos. Continua, no entanto, a ser um identificador administrativo: não deve ser hardcoded, publicado em logs, colocado em issues públicas ou incluído em capturas sem necessidade.

Nunca coloque nestas variáveis:

- número de cartão;
- CVV;
- passwords;
- tokens OAuth;
- conteúdo de ADC;
- chaves JSON de service accounts.
