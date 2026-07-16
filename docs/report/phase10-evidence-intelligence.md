# Fase 10 — inteligência e prontidão da evidência

## Execução isolada

```powershell
& .\scripts\evidence\collect-evidence-intelligence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -RunId "YYYYMMDDTHHMMSSZ" `
  -Overwrite
```

Os wrappers PowerShell e shell executam a Fase 10 depois de a campanha e o respetivo verificador terminarem. Uma chamada direta ao runner Python deve ser seguida pela execução isolada abaixo. A fase não altera os outputs anteriores.

## Outputs principais

- `evidence-index.*`: catálogo de todos os artefactos;
- `integrity-audit.*`: verificação de manifests SHA-256;
- `phase-scorecard.*`: cobertura e estado por fase;
- `claim-lineage.*`: ligação claim–fonte–classe–integridade;
- `figure-inventory.*`: formatos e cobertura das figuras;
- `report-asset-audit.*`: confirmação de assets prontos para relatório;
- `evidence-gap-register.*`: lacunas priorizadas;
- `phase10-summary.*`: prontidão global;
- `report-ready/`: tabelas, scorecard e diagramas para integração.

## Interpretação do estado

- `READY_TO_SHARE`: pacote íntegro e rastreável segundo as regras configuradas;
- `SHARE_WITH_CAVEATS`: utilizável, mas com limitações que devem acompanhar os resultados;
- `NEEDS_REVISION`: existem falhas materiais, como hashes incorretos, fontes de claims ausentes ou fases selecionadas sem resumo.

Este estado avalia o pacote de evidência e não a validade científica ou operacional do sistema.
