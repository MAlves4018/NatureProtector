# Fase 2 — recolha atual de testes, qualidade e cobertura

## Finalidade

A Fase 2 substitui contagens estáticas por resultados de execução preservados. O coletor regista o ambiente, os comandos, os códigos de saída, a duração, os resultados de testes e a cobertura disponível para backend e frontend.

A recolha não executa Git, não altera `.env` ou `.env.example`, não inicia Docker por omissão e não interage com cloud. Os testes `Category=DockerIntegration` e os testes Playwright permanecem separados para evitar apresentar uma execução parcial como prova integrada.

## Execução recomendada no Windows

A partir da raiz do repositório, em PowerShell:

```powershell
& .\scripts\evidence\collect-test-quality-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe"
```

O wrapper executa, por omissão:

1. `dotnet tool restore`;
2. `dotnet restore NatureProtector.sln`;
3. `dotnet build NatureProtector.sln -c Release --no-restore`;
4. `dotnet test` com TRX, Cobertura e exclusão de `DockerIntegration`;
5. relatório agregado de cobertura do backend;
6. `npm ci`;
7. verificação do toolchain frontend;
8. typecheck, lint e format check;
9. Vitest com cobertura;
10. build de produção;
11. normalização dos resultados para JSON/CSV/Markdown;
12. verificação de coerência e de todos os hashes SHA-256.

## Execução no Git Bash ou Linux

```bash
bash scripts/evidence/collect-test-quality-evidence.sh \
  --baseline-id baseline-YYYYMMDDTHHMMSSZ
```

Para fixar explicitamente o Python:

```bash
bash scripts/evidence/collect-test-quality-evidence.sh \
  --baseline-id baseline-YYYYMMDDTHHMMSSZ \
  --python /c/Users/Miguel/AppData/Local/Programs/Python/Python313/python.exe
```

## Opções úteis

```text
--skip-backend       Não executa o backend e regista a decisão.
--skip-frontend      Não executa o frontend e regista a decisão.
--skip-npm-ci        Reutiliza node_modules já existente.
--include-e2e        Acrescenta Playwright; requer os pré-requisitos do runtime.
--no-restore         Não executa dotnet restore.
--no-build           Não executa dotnet build.
--timeout-seconds N  Limite por comando; valor por omissão: 1800.
--quiet              Preserva os logs sem repetir todo o output no terminal.
```

## Outputs

Os resultados são escritos em:

```text
artifacts/report-evidence/<baseline-id>/02-tests/<run-id>/
```

Principais ficheiros:

- `phase2-summary.md` e `phase2-summary.json`;
- `environment.json`;
- `command-results.csv` e `command-results.json`;
- `backend/test-results.csv`;
- `backend/coverage-by-assembly.csv`;
- `backend/test-results/**/*.trx` e Cobertura bruto, quando o .NET é executado;
- `backend/coverage-report/`, quando o ReportGenerator é executado;
- `frontend/test-results.csv`;
- `frontend/raw-test-results/vitest-junit.xml`;
- `frontend/coverage/coverage-summary.json` e `cobertura-coverage.xml`;
- logs individuais em `logs/`;
- `SHA256SUMS.txt`.

## Estados autorizados

- `PASS`: backend e frontend obrigatórios executaram e passaram.
- `PARTIAL_PASS_BLOCKED_ENVIRONMENT`: uma camada passou e a outra não pôde ser executada por ausência de ferramenta compatível.
- `BLOCKED`: nenhuma camada obrigatória pôde ser executada.
- `FAIL`: pelo menos um comando obrigatório executado falhou.

Um estado `BLOCKED` não pode ser convertido editorialmente em `PASS`. A ausência de .NET, Docker, browser ou outro pré-requisito deve permanecer explícita.

## Limite das afirmações

A execução padrão do backend exclui `DockerIntegration`; por isso, não prova integração real com PostgreSQL, RabbitMQ ou InfluxDB. Os testes Vitest não provam o percurso completo no browser. Cobertura mede código instrumentado percorrido, não prova ausência de defeitos, capacidade de produção ou validade científica.

## Verificação isolada

```bash
python3 scripts/evidence/verify-test-quality-evidence.py \
  --evidence-root artifacts/report-evidence/baseline-YYYYMMDDTHHMMSSZ/02-tests/<run-id>
```

O resultado correto termina em:

```text
PHASE_2_VERIFICATION=PASS
```
