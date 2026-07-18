# Protocolo de capturas para evidência visual

## Capturas prioritárias

1. criação e configuração de uma run;
2. estado da run e motivo de conclusão;
3. timeline por módulo;
4. mensagens filtradas por `SimulationRunId`, `CycleIndex` e `GridCellId`;
5. detalhe de uma avaliação com `BaseRisk`, score, confiança e integridade;
6. alerta aberto, mantido e fechado por histerese;
7. degradação introduzida e efeito observado;
8. métricas de RabbitMQ, PostgreSQL e Influx/Grafana;
9. comparação entre cenários A, B e C;
10. reconstrução completa de uma ocorrência.

## Preparação

- usar uma resolução repetível, preferencialmente 1920×1080;
- ocultar tokens, nomes pessoais, DSN e identificadores irrelevantes;
- preservar timestamps, filtros, cenário e run;
- não cortar avisos ou estados de erro;
- evitar dados de runs diferentes no mesmo enquadramento;
- confirmar que a UI terminou de carregar.

## Registo obrigatório

Depois de capturar a imagem, usar:

```powershell
python scripts/evidence/register-evidence-capture.py `
  --image ".\capture.png" `
  --evidence-root ".\artifacts\report-evidence\<baseline>\04-runtime\<run>" `
  --title "Timeline da execução do cenário C" `
  --purpose "Demonstrar rastreabilidade modular e degradação" `
  --chapter-target "Capítulo 6 — comparação A/B/C" `
  --baseline-id "<baseline>" `
  --run-id "<run>" `
  --source-page "/operations/runs/<id>" `
  --scenario "C" `
  --simulation-run-id "<SimulationRunId>"
```

O comando cria uma cópia imutável, `metadata.json`, `SHA256SUMS.txt` e atualiza `capture-register.*`.

## Legenda recomendada

A legenda deve identificar cenário, run e objetivo, mas não repetir todos os filtros técnicos. Exemplo:

> Evolução das avaliações durante o cenário C, com redução de integridade após introdução de mensagens em falta. Run controlada `<run-label>`; os valores representam a mesma configuração física usada no cenário B.

## Capturas que não devem ser usadas como prova isolada

- uma página sem timestamps;
- um gráfico sem unidade;
- um estado “verde” sem detalhe do gate;
- um log cortado antes do erro;
- uma métrica agregada sem população;
- uma imagem de configuração sem resultado da execução.
