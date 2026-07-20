# Fase 13 — execução final integrada da evidência

## Finalidade

A Fase 13 fecha a lacuna entre os coletores existentes e uma execução final previsível.
Não cria um segundo sistema em `scripts/evidence-final`: reutiliza as fases já presentes,
os seus verificadores, classes de evidência, limites de afirmação, manifests e integração
com o relatório.

## Ordem canónica

```text
preflight e runtime local
→ Fase 8 (Fases 1–6, 9, 11 e 7)
→ portefólio E1–E6
→ matriz de longa duração
→ capturas Playwright registadas
→ Fase 13
→ Fase 10 atualizada por último
```

A Fase 10 é repetida no fim para indexar a Fase 13. Os outputs das fases anteriores não
são alterados nem promovidos.

## Modos

- `Plan`: gera os planos da Fase 8 e do portefólio E1–E6 sem executar runtime.
- `Quick`: executa o perfil `quality` da campanha existente e produz uma Fase 13 parcial.
- `Full`: inicia ou reutiliza o runtime, executa a campanha completa, E1–E6, longa duração
  e capturas de interface.
- `AnalyzeOnly`: liga e verifica diretórios já recolhidos, sem iniciar serviços.

## Comandos

### Verificação inicial

```powershell
.\scripts\evidence\Invoke-NP-FinalEvidence.ps1 `
  -Mode Plan `
  -BaselineId "baseline-20260718-final"
```

### Campanha rápida

```powershell
.\scripts\evidence\Invoke-NP-FinalEvidence.ps1 `
  -Mode Quick `
  -BaselineId "baseline-20260718-final" `
  -BootstrapIterations 100
```

### Campanha completa local

Antes de executar, definir apenas por variáveis de ambiente os valores necessários pelo
runtime e pelos coletores existentes, incluindo as credenciais temporárias da UI/API,
PostgreSQL, RabbitMQ, InfluxDB e Grafana. Os valores não são escritos na configuração.

```powershell
.\scripts\evidence\Invoke-NP-FinalEvidence.ps1 `
  -Mode Full `
  -BaselineId "baseline-20260718-final" `
  -AllowReviewedCommands `
  -AcknowledgeNonProduction `
  -RequireLive
```

Para um runtime já iniciado:

```powershell
.\scripts\evidence\Invoke-NP-FinalEvidence.ps1 `
  -Mode Full `
  -BaselineId "baseline-20260718-final" `
  -UseExistingRuntime `
  -AllowReviewedCommands
```

## Matriz de longa duração

A matriz final mantém quatro execuções assíncronas, duas síncronas e uma rejeição de
configuração. O runner passa a aceitar resultados esperados diferentes de
`SystemCompleted`, sem tratar um timeout configurado ou uma rejeição válida como crash do
harness. Cada caso produz manifestos de terminação, timeline, observações de processo,
correlação por `OperationId`/`SimulationRunId` e hashes.

## Capturas

A Fase 13 chama o teste `webUI/e2e/live-runtime.spec.ts`, já existente, nos perfis
`nominal` e `missing`. As imagens são depois registadas com
`register-evidence-capture.py`, preservando baseline, run, cenário, página, propósito,
limitações e SHA-256.

## Limites

- Uma Fase 13 aprovada não valida cientificamente o `NP_score`.
- A Fase 13 não converte execução sintética, planeada ou histórica em execução atual.
- Screenshots não equivalem a validação de usabilidade com operadores.
- A classe e o teto de afirmação continuam a ser definidos pelas fases de origem.
