# Setup local Google Cloud

## Objetivo

Preparar uma estação Windows para trabalhar no NatureProtector sem criar infraestrutura e sem distribuir credenciais permanentes.

## 1. Pré-requisitos

Obrigatório:

- PowerShell;
- Google Cloud CLI;
- conta Google com acesso IAM ao projeto.

Conforme a função:

- Terraform para infraestrutura;
- Docker para construir ou consumir imagens;
- `kubectl` e o plugin de autenticação GKE para Kubernetes;
- Git e GitHub CLI para o repositório e workflows.

O scaffold verifica estas ferramentas, mas não as instala automaticamente, porque alguns instaladores exigem direitos administrativos, reinício ou decisões locais.

## 2. Permissões

O owner atribui permissões IAM fora destes scripts. Um colaborador não recebe `Owner` automaticamente e não deve receber Billing Account Administrator apenas para desenvolver.

O setup local não consegue conceder permissões que o utilizador ainda não tenha.

## 3. Configuração

O owner administrativo pode usar `Set-CloudEnvironment.ps1`. Um colaborador normal precisa apenas de Project ID, região e nome da configuração.

```powershell
$env:NATUREPROTECTOR_PROJECT_ID = "natureprotector-500518"
$env:NATUREPROTECTOR_REGION = "europe-southwest1"
$env:NATUREPROTECTOR_GCLOUD_CONFIGURATION = "natureprotector-dev"
```

## 4. Login e defaults

```powershell
.\scripts\cloud\Setup-CloudDeveloper.ps1 `
  -Account "<EMAIL>" `
  -ConfigureDocker
```

O script:

1. confirma as ferramentas;
2. cria ou ativa uma configuração gcloud isolada;
3. autentica a conta humana;
4. define conta, projeto e região;
5. confirma acesso ao projeto;
6. configura ADC para desenvolvimento local;
7. define o quota project;
8. opcionalmente configura Docker para o Artifact Registry regional.

## 5. Validação

```powershell
.\scripts\cloud\Test-CloudSetup.ps1
```

Para exigir todo o toolchain:

```powershell
.\scripts\cloud\Test-CloudSetup.ps1 `
  -RequireTerraform `
  -RequireDocker `
  -RequireKubectl
```

O teste é read-only e não imprime tokens ADC. Produz evidence JSON local.

## 6. O que não fazer

- Não copiar o ficheiro ADC para o repositório.
- Não definir `GOOGLE_APPLICATION_CREDENTIALS` para uma chave JSON permanente.
- Não partilhar a pasta de configuração gcloud.
- Não usar a conta pessoal de outro membro.
- Não executar `terraform apply` durante o setup local.
- Não usar o projeto de production para desenvolvimento.
