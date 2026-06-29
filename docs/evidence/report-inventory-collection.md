# Recolha automática do inventário usado no relatório

## Finalidade

O coletor da Fase 1 transforma o estado estático do repositório em datasets CSV e JSON reproduzíveis. Recolhe projetos, ficheiros e linhas, declarações de testes, endpoints, eventos, telemetria, migrações, modelo PostgreSQL, workflows, serviços Compose e configuração do frontend.

A recolha é **estática**. Não executa testes, serviços, Docker, bases de dados, cloud ou benchmarks e não altera `.env`, `.env.example` ou código funcional.

## Execução no Git Bash

A partir da raiz do repositório:

```bash
bash scripts/evidence/collect-report-inventory.sh \
  --baseline-id baseline-YYYYMMDDTHHMMSSZ
```

Para fixar explicitamente o Python:

```bash
bash scripts/evidence/collect-report-inventory.sh \
  --baseline-id baseline-YYYYMMDDTHHMMSSZ \
  --python /c/Users/Miguel/AppData/Local/Programs/Python/Python313/python.exe
```

## Execução no PowerShell

```powershell
& .\scripts\evidence\collect-report-inventory.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe"
```

Quando `-BaselineId`/`--baseline-id` não é fornecido, os wrappers tentam obtê-lo de `artifacts/report-evidence/LATEST.txt`.

## Resultado

Por omissão, os ficheiros são escritos em:

```text
artifacts/report-evidence/<baseline-id>/01-inventory/
```

Os principais outputs são:

- `inventory-summary.md` e `inventory-summary.json`;
- `projects.csv`;
- `source-inventory.csv`;
- `test-inventory.csv`;
- `endpoints.csv`;
- `event-catalog.csv`;
- `telemetry-metrics.csv`, `telemetry-activities.csv` e `telemetry-tags.csv`;
- `migrations.csv`;
- `database-schemas.csv`, `database-tables.csv`, `database-columns.csv` e `database-indexes.csv`;
- `workflows.csv`;
- `compose-services.csv`;
- `frontend-inventory.json`;
- `inventory.json` consolidado;
- `SHA256SUMS.txt`.

## Limite das afirmações

As contagens representam declarações existentes no snapshot analisado. Não devem ser apresentadas como execução atual. Por exemplo, `893` atributos `Fact` não significa automaticamente `893` testes executados e uma tabela declarada numa migração não prova que existe numa base de dados ativa.

## Verificação independente dos outputs

Os wrappers executam automaticamente o verificador após a recolha. Também pode ser executado isoladamente:

```bash
python3 scripts/evidence/verify-report-inventory.py \
  --inventory-root artifacts/report-evidence/baseline-YYYYMMDDTHHMMSSZ/01-inventory
```

O resultado correto termina em `PHASE_1_VERIFICATION=PASS`. O verificador confirma consistência entre JSON e CSV, ausência de duplicados nas chaves principais e todos os hashes do `SHA256SUMS.txt`.
