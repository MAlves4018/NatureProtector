# Testes

Esta pasta contém os projetos de teste da solução. O panorama atual é assimétrico: já existe boa cobertura no domínio central, mas ainda há módulos com projetos de teste criados sem corpo relevante.

## Projetos existentes

- [NatureProtector.Core.Tests](NatureProtector.Core.Tests)
  - contém testes reais para áreas, primitivas, leituras, risco, cenários, sensores e meteorologia
- [NatureProtector.Prevention.Tests](NatureProtector.Prevention.Tests)
  - projeto criado, mas ainda sem testes substantivos
- [NatureProtector.Shared.Tests](NatureProtector.Shared.Tests)
  - projeto criado, mas ainda sem testes substantivos

## O que isto nos diz

- O domínio em `NatureProtector.Core` é hoje a parte melhor defendida por testes.
- `NatureProtector.Prevention` e `NatureProtector.Shared` ainda precisam de cobertura para contratos, serialização, scoring e integração de pipeline.
- Ainda não existe um projeto de testes de integração end-to-end alinhado com o desenho de arquitetura que já está documentado.

## Como executar

Para correr todos os testes disponíveis, devemos executar:

```powershell
dotnet test NatureProtector.sln
```

Se quisermos focar apenas o domínio, devemos executar:

```powershell
dotnet test .\tests\NatureProtector.Core.Tests\NatureProtector.Core.Tests.csproj
```

## Relação com a documentação de planeamento

O roadmap em [../docs/planning/project-completion-roadmap.md](../docs/planning/project-completion-roadmap.md) já antecipa a necessidade de aumentar a cobertura em contratos, simulação, pipeline e integração. Esta pasta mostra, de forma muito concreta, que esse trabalho continua em aberto.
