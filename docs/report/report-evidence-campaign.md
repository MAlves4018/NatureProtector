# Campanha canónica de evidência do relatório

A Fase 8 fornece um único orquestrador para executar, na ordem correta, os coletores das Fases 1–7. O objetivo é evitar comandos soltos, resultados misturados entre versões e promoção acidental de evidência estática ou histórica.

## Regras de segurança

- O modo predefinido é apenas planeamento; sem `-Execute` nenhum teste, serviço, base de dados, benchmark ou P3 é executado.
- Credenciais são lidas apenas de variáveis de ambiente e nunca são escritas no output.
- `-ExecuteP3` e `-ResetRuntime` exigem `-AcknowledgeNonProduction`.
- A campanha não executa Git, cloud, deployment ou alterações de schema.
- Cada fase continua a definir a sua própria classe de evidência e limite de afirmação.

## 1. Ver o plano e o preflight

```powershell
& .\scripts\evidence\run-report-evidence-campaign.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -Profile full `
  -PythonExecutable "python"
```

O comando cria `08-campaign/<run-id>/preflight.json` e `execution-plan.*`, mas não executa as fases.

## 2. Recolha estática portátil

```powershell
& .\scripts\evidence\run-report-evidence-campaign.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -Profile static `
  -Execute `
  -PythonExecutable "python"
```

Executa inventário, arquitetura SQL estática e regeneração dos artefactos prontos para o relatório.

## 3. Testes e cobertura

```powershell
& .\scripts\evidence\run-report-evidence-campaign.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -Profile quality `
  -Execute `
  -PythonExecutable "python"
```

Executa também backend/frontend, cobertura e gates de qualidade. E2E permanece opcional através de `-IncludeE2E`.

## 4. Campanha completa no runtime local

Antes da execução:

```powershell
$env:NATUREPROTECTOR_POSTGRES_DSN = "<DSN temporário>"
$env:NATUREPROTECTOR_RUNTIME_BEARER_TOKEN = "<token temporário>"
$env:NP_RELIABILITY_AUTH_TOKEN = "<token temporário Sim/Admin>"
```

Depois:

```powershell
$runLabel = "controlled-validation-p3-negative-pipeline-$((Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss'))-report"

& .\scripts\evidence\run-report-evidence-campaign.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -Profile full `
  -Execute `
  -ApiBaseUrl "http://localhost:5254" `
  -RunHttp `
  -HttpProfile B1 `
  -RunMicrobenchmarks `
  -BenchmarkProfile B1 `
  -RequireLiveDatabase `
  -RequireLiveRuntime `
  -ExecuteP3 `
  -AcknowledgeNonProduction `
  -P3RunLabel $runLabel `
  -RequireP3 `
  -PythonExecutable "python"
```

A auditoria PostgreSQL do P3 continua a ser um segundo passo deliberado. Depois de executar `tools/data-audit/run-postgres-audit.ps1`, volta a correr a campanha com `-AuditDirectory <...\p3\postgres> -RequireAudit`.

## 5. Workload sistémico

O workload sistémico continua separado porque é mais demorado e produz o seu próprio diretório:

```powershell
& .\scripts\performance\run-system-capacity-workload.ps1 `
  -Profile Calibration `
  -UseDevelopmentAdminDefault

& .\scripts\performance\run-system-capacity-workload.ps1 `
  -Profile B1 `
  -Repetitions 10 `
  -UseDevelopmentAdminDefault `
  -CalibrationRunDirectory ".\artifacts\performance\<calibration-run>"
```

Importa depois o diretório B1 na campanha através de `-SystemRunDirectory` e, quando aplicável, `-RequireSystem`.

## Output

Cada campanha escreve:

- `preflight.json`;
- `execution-plan.json/csv`;
- logs por fase;
- `step-results.csv`;
- `campaign-summary.json/md`;
- `SHA256SUMS.txt`.

O verificador falha perante hashes incorretos, estrutura inconsistente ou material com aparência de segredo.

## 6. Validação exploratória do NP_score (Fase 9)

Os perfis `static`, `quality` e `full` executam agora a Fase 9. Esta fase é
estática quanto à reconstrução histórica e, no perfil `full`, importa também
os diretórios produzidos pelas Fases 4–6 para comparar métricas atribuíveis a
cenários atuais. A ordem canónica é `Fases 4–6 → Fase 9 → Fase 7`: a análise é
primeiro verificada e só depois convertida em tabelas LaTeX, figuras, claims e
texto pronto para integração no relatório.

Execução isolada:

```powershell
& .\scripts\evidence\collect-np-score-validation.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -RunId "YYYYMMDDTHHMMSSZ" `
  -RequireComplete
```

A configuração canónica fica em
`config/evidence/np-score-validation.json`. O verificador impede promoção para
“probabilidade calibrada”, “validação causal” ou generalização externa. A Fase
7 reconhece estes resultados como `CURRENT_ANALYTICAL_EVIDENCE`, uma classe
separada de execução runtime e de verificação estática.

## 7. Governação e prontidão do pacote (Fase 10)

Depois de a Fase 7 produzir tabelas, figuras e claims, a Fase 10 percorre toda a
baseline e verifica:

- cobertura das fases selecionadas;
- integridade dos manifests SHA-256;
- existência e classe das fontes dos claims;
- disponibilidade dos assets de relatório;
- formatos das figuras;
- lacunas que impedem ou condicionam a apresentação.

A ordem canónica completa passa a ser:

`Fases 1–6 → Fase 9 → Fase 7 → Fase 10`.

Os wrappers executam esta pós-validação apenas depois de `campaign-summary.*` e `SHA256SUMS.txt` da Fase 8 estarem concluídos. A Fase 10 produz um scorecard de prontidão, mas não altera o limite de afirmação
definido pelas fases produtoras. Um pacote pode estar tecnicamente bem
organizado e continuar a conter apenas evidência exploratória ou estática.


## Fase 11 — fecho de lacunas

A campanha executa a Fase 11 depois da validação do NP_score e antes da integração documental. A fase admite fontes históricas B/C verificáveis, calcula cobertura real e gera o runbook para as áreas ainda bloqueadas.
