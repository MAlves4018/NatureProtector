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
  -PythonExecutable "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe"
```

O comando cria `08-campaign/<run-id>/preflight.json` e `execution-plan.*`, mas não executa as fases.

## 2. Recolha estática portátil

```powershell
& .\scripts\evidence\run-report-evidence-campaign.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -Profile static `
  -Execute `
  -PythonExecutable "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe"
```

Executa inventário, arquitetura SQL estática e regeneração dos artefactos prontos para o relatório.

## 3. Testes e cobertura

```powershell
& .\scripts\evidence\run-report-evidence-campaign.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -Profile quality `
  -Execute `
  -PythonExecutable "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe"
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
  -PythonExecutable "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe"
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
