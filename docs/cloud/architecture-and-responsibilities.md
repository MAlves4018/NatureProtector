# Arquitetura do setup e responsabilidades

## Owner/cloud administrator

Responsável por:

- billing;
- budgets;
- ligação dos projetos;
- IAM;
- service accounts;
- Workload Identity Federation;
- state de Terraform;
- criação e destruição de ambientes;
- auditoria de custos.

Usa:

- `Set-CloudEnvironment.ps1`;
- `Initialize-CloudProject.ps1`;
- `Test-CloudSetup.ps1`;
- futuramente os módulos Terraform reais.

## Developer

Responsável por:

- autenticar a própria conta;
- usar a configuração gcloud correta;
- obter ADC para desenvolvimento local;
- usar apenas as permissões concedidas;
- não criar recursos administrativos.

Usa:

- `Setup-CloudDeveloper.ps1`;
- `Test-CloudSetup.ps1`.

Não precisa normalmente do Billing Account ID.

## CI/CD

Responsável por:

- construir, assinar e publicar artefactos;
- executar deployments autorizados;
- usar credenciais temporárias.

Não deve usar:

- login humano;
- ficheiros ADC locais;
- chaves JSON permanentes;
- Billing Account ID para deployments normais.

A autenticação prevista é Workload Identity Federation com condições de confiança limitadas ao GitHub.

## Separação entre setup e infraestrutura

O setup desta entrega configura apenas o plano de controlo:

```text
identidade local
→ projeto/região
→ billing verificada
→ budget
→ APIs base
→ validação/evidence
```

A infraestrutura real fica separada:

```text
Terraform plan
→ revisão de custo e recursos
→ teardown preparado
→ apply staging
→ deployment
→ testes/evidence
→ destroy staging
```

Esta separação impede que “preparar o computador” crie acidentalmente GKE, Cloud SQL ou load balancers.
