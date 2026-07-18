# Recolha de evidência PostgreSQL e ERD para o relatório

Esta automação corresponde à Fase 3 da campanha de evidência do NatureProtector. Produz um modelo físico atual a partir do repositório e, quando é fornecida uma ligação explícita, recolhe também metadados read-only de uma instância PostgreSQL.

## Evidência separada por força

A execução distingue sempre:

1. `STATIC_EFFECTIVE_DATABASE_MODEL` — reconstruído do `NatureProtectorControlDbContextModelSnapshot.cs` e das migrações SQL em bruto ainda não refletidas nesse snapshot;
2. `CURRENT_LIVE_DATABASE_INVENTORY` — catálogo, tamanhos e estatísticas recolhidos de uma instância PostgreSQL atual.

O primeiro permite afirmar o que a versão do código declara. Só o segundo permite afirmar o que está realmente aplicado e materializado numa base em execução.

## Execução normal no Windows

No PowerShell, a partir da raiz do repositório:

```powershell
& .\scripts\evidence\collect-database-architecture-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "python"
```

Este modo não necessita de PostgreSQL, Docker ou .NET. Gera o ERD, inventários estáticos e o catálogo de queries críticas.

## Execução com PostgreSQL atual

A ligação deve apontar para uma instância isolada da campanha de evidência. O DSN não é preservado com password nos outputs.

```powershell
& .\scripts\evidence\collect-database-architecture-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "python" `
  -Dsn $env:NATUREPROTECTOR_POSTGRES_DSN `
  -RequireLive
```

Pré-requisito Python para live mode:

```powershell
& "python" `
  -m pip install "psycopg[binary]>=3.2,<4"
```

A ligação é aberta com `default_transaction_read_only=on` e `statement_timeout=60000`.

## Execução no Git Bash

```bash
scripts/evidence/collect-database-architecture-evidence.sh \
  baseline-YYYYMMDDTHHMMSSZ \
  --python /c/Users/Miguel/AppData/Local/Programs/Python/Python313/python.exe
```

Com base live:

```bash
scripts/evidence/collect-database-architecture-evidence.sh \
  baseline-YYYYMMDDTHHMMSSZ \
  --python /c/Users/Miguel/AppData/Local/Programs/Python/Python313/python.exe \
  --dsn "$NATUREPROTECTOR_POSTGRES_DSN" \
  --require-live
```

## Outputs

A execução escreve em:

```text
artifacts/report-evidence/<baseline>/03-database/<run-id>/
```

Principais artefactos:

- `static/schema-model.json` — modelo consolidado;
- `static/schemas.csv`;
- `static/tables.csv`;
- `static/columns.csv`;
- `static/primary-keys.csv`;
- `static/foreign-keys.csv`;
- `static/indexes.csv`;
- `diagrams/erd-full.svg` e `.png`;
- `diagrams/erd-report-simplified.svg` e `.png`;
- fontes Graphviz `.dot` e Mermaid `.mmd`;
- `queries/critical-query-catalog.csv`;
- `queries/critical-queries-explain.sql`;
- `report-ready/database-summary.csv`;
- `live/table-statistics.csv`, quando live mode passa;
- `SHA256SUMS.txt`.

## Queries críticas e planos

O ficheiro `critical-queries-explain.sql` contém doze percursos candidatos e templates de:

```sql
EXPLAIN (ANALYZE, BUFFERS, WAL, SETTINGS, FORMAT JSON)
```

Os parâmetros devem ser substituídos por IDs reais e representativos. Estes planos não são executados automaticamente nesta fase, porque `EXPLAIN ANALYZE` executa a query e deve ser recolhido apenas numa base isolada, com workload e parâmetros documentados.

## Regras de interpretação

Pode afirmar-se, com a execução estática:

- quantos schemas, tabelas, colunas, PK, FK e índices a versão declara;
- que o ERD foi gerado do modelo efetivo reconstruído;
- quais queries foram selecionadas para medição posterior.

Não pode afirmar-se sem live mode:

- que todas as migrações estão aplicadas;
- que a base atual contém exatamente os objetos declarados;
- quantas linhas ou bytes existem;
- que um índice é usado;
- que uma query cumpre um objetivo de latência.

## Verificação independente

```powershell
& "python" `
  .\scripts\evidence\verify-database-architecture-evidence.py `
  .\artifacts\report-evidence\baseline-YYYYMMDDTHHMMSSZ\03-database\<run-id>
```

Resultado esperado para a recolha estática:

```text
PHASE_3_VERIFICATION=PASS
```
