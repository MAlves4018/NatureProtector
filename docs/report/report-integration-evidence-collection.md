# Recolha de integração de evidência no relatório — Fase 7

A Fase 7 agrega os resultados das Fases 1–6 em tabelas, figuras, fragmentos LaTeX e num registo afirmação–evidência. Não executa a aplicação, testes, bases de dados, benchmarks ou cloud e não eleva a força das fontes de origem.

## Execução

### Git Bash

```bash
bash scripts/evidence/collect-report-integration-evidence.sh \
  "$PWD" \
  baseline-YYYYMMDDTHHMMSSZ
```

### PowerShell

```powershell
& .\scripts\evidence\collect-report-integration-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe"
```

## Outputs

Os resultados ficam em:

```text
artifacts/report-evidence/<baseline>/07-report-integration/<run>/
```

Incluem:

- oito tabelas em CSV, Markdown e LaTeX;
- cinco figuras em SVG e PNG;
- ERD simplificado copiado da Fase 3;
- mapa de integração por capítulo/anexo;
- registo de afirmações, fontes, formulações autorizadas e proibidas;
- texto de síntese pronto para revisão editorial;
- manifesto e hashes SHA-256.

## Regra de promoção

Uma tabela ou figura mantém a classe da evidência de origem. Em particular:

- a execução frontend é atual;
- o modelo SQL é verificação estática, não introspeção live;
- B/C é histórico;
- performance e P3 permanecem implementados mas não executados;
- bloqueios não podem ser convertidos em resultados positivos.
