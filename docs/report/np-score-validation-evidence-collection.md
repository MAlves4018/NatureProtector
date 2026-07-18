# Fase 9 — recolha de evidência para validação exploratória do NP_score

## Objetivo

A Fase 9 avalia se o `Candidate Parameter Set V1.0` apresenta comportamento coerente e capacidade de ordenação retrospectiva perante dados históricos. Não converte o `NP_score` numa probabilidade e não promove a expressão “cientificamente validado”.

A fase foi desenhada para reutilizar a infraestrutura de evidência existente:

- utiliza o mesmo `BaselineId` e `RunId` da campanha do relatório;
- escreve apenas em `artifacts/report-evidence/<baseline>/09-np-score-validation/<run>`;
- produz resumo JSON/Markdown, CSV auditáveis, SVG acessíveis, proveniência e `SHA256SUMS.txt`;
- possui coletor e verificador separados;
- importa resultados das Fases 4–6 quando estes existem, sem transformar ausência de runtime em execução atual.

## Classes de evidência

| Classe | Fonte | Afirmação permitida |
|---|---|---|
| Estática reproduzida | Código C# e configuração | A fórmula analisada coincide com a versão do código. |
| Retrospectiva exploratória | Weather daily, histórico de incêndios e células | Discriminação, sensibilidade, ablação e alinhamento de limiares na área/período avaliados. |
| Runtime importada | Outputs atuais das Fases 4–6 | Comparação de cenários apenas para métricas efetivamente encontradas e atribuíveis a uma run. |
| Não demonstrada | Dados externos/operacionais não presentes | Probabilidade calibrada, causalidade, eficácia operacional e generalização nacional. |

## Execução autónoma

### PowerShell

```powershell
& .\scripts\evidence\collect-np-score-validation.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -RunId "YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "python" `
  -RequireComplete
```

Para importar evidência runtime já recolhida:

```powershell
& .\scripts\evidence\collect-np-score-validation.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -RuntimeEvidenceRoot @(
    ".\artifacts\report-evidence\baseline-...\04-runtime\run-...",
    ".\artifacts\report-evidence\baseline-...\05-performance\run-...",
    ".\artifacts\report-evidence\baseline-...\06-reliability\run-..."
  ) `
  -RequireComplete
```

### Linux/macOS

```bash
./scripts/evidence/collect-np-score-validation.sh \
  --baseline-id baseline-YYYYMMDDTHHMMSSZ \
  --run-id YYYYMMDDTHHMMSSZ \
  --config config/evidence/np-score-validation.json
```

## Execução pela campanha canónica

Os perfis `static`, `quality` e `full` incluem a Fase 9. No perfil `full`, a Fase 9 é executada depois das Fases 4–6 para poder importar os artefactos atuais de runtime, desempenho e fiabilidade. Em todos estes perfis, a Fase 9 antecede a Fase 7, que consome o `phase9-summary.json`, as tabelas e as figuras verificadas para construir o pacote de integração do relatório.

```powershell
& .\scripts\evidence\run-report-evidence-campaign.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -Profile static `
  -Execute `
  -NpScoreBootstrapIterations 500
```

## Metodologia implementada

1. **Congelamento lógico da fórmula** — os valores de `config/evidence/np-score-validation.json` são comparados com `CandidateParameterSetV1.cs`. A run falha perante drift.
2. **Reconstrução territorial** — perigosidade, combustível e geomorfologia são calculados segundo `TerritorialRiskContext.cs` para todas as células.
3. **Reconstrução diária** — temperatura, humidade, vento, FWI e KBDI alimentam a mesma composição da implementação C#.
4. **Agregação de área** — `0,70 × p80 + 0,30 × máximo`, usando nearest rank, como em `AreaRiskSnapshot.cs`.
5. **Definição de eventos** — são usadas datas de início dos tipos históricos autorizados; ocorrências múltiplas na mesma data são colapsadas para o label binário.
6. **Controlos sazonais** — população maio–outubro e controlos emparelhados pelo mês, excluindo a janela temporal configurada em redor dos eventos.
7. **Comparação de modelos** — NP_score, score meteorológico simples, FWI, KBDI, índice combinado e variantes de ablação.
8. **Incerteza** — intervalos bootstrap estratificados para ROC-AUC e average precision.
9. **Limiar operacional** — sensibilidade, especificidade, precisão, dias positivos e falsos alertas por 30 dias.
10. **Estabilidade** — variações dos pesos, blend de FWI e referência de normalização.
11. **Validação temporal** — 2017–2022, holdout 2023–2025 e cortes anuais.
12. **Importação runtime** — descoberta conservadora de registos com cenário e métricas conhecidas; nenhuma conclusão é promovida quando os artefactos não existem.

## Outputs

| Artefacto | Conteúdo |
|---|---|
| `formula-contract.json` | Comparação entre configuração e constantes C#. |
| `daily-score-dataset.csv` | Dataset diário reconstruído, componentes, baselines e labels. |
| `territorial-components.csv` | Componentes territoriais por célula e limitações. |
| `model-comparison.csv` | ROC-AUC, AP, IC95%, distribuições e efeito. |
| `matched-controls.csv` | Relação determinística evento–controlo. |
| `threshold-analysis.csv` | Trade-off completo e limiares 0,50/0,60/0,70/0,80. |
| `temporal-validation.csv` | Exploração, holdout e resultados anuais. |
| `component-correlations.csv` | Correlação entre score e componentes/baselines. |
| `sensitivity-analysis.csv` | Robustez perante alterações de parâmetros. |
| `extent-association.json` | Associação exploratória com extensão ardida. |
| `scenario-comparison.csv` | Métricas runtime importadas por cenário, se disponíveis. |
| `data-quality.json` | Cobertura, missingness, duplicados e limitações. |
| `phase9-summary.*` | Resultado principal e limites de afirmação. |
| `figures/*.svg` | Distribuição, ROC, PR, limiares e sensibilidade. |
| `provenance.json` | Fontes, hashes, seeds e parâmetros da execução. |
| `SHA256SUMS.txt` | Integridade de todos os artefactos da run. |

## Regras para o relatório

Podem ser apresentadas:

- capacidade discriminante retrospectiva;
- comparação com baselines;
- estabilidade dos pesos;
- desalinhamento dos limiares;
- limitações e requisitos de calibração futura.

Não podem ser apresentadas sem nova evidência:

- “70 pontos significam 70% de probabilidade”;
- “o modelo foi cientificamente validado”;
- “o NP_score prevê a área ardida”;
- “o modelo generaliza para Portugal”;
- “o score causa melhores decisões operacionais”.
