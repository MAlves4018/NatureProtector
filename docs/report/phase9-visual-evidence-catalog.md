# Catálogo visual da Fase 9 — validação do NP_score

## Objetivo

Este catálogo separa as figuras que explicam o método das figuras que apresentam resultados. A seleção evita repetir gráficos, evita transformar correlação em validação e garante que cada figura tem uma mensagem verificável.

## Figuras recomendadas para o corpo do Capítulo 6

| Prioridade | Figura | Ficheiro | Mensagem principal | Estado |
|---|---|---|---|---|
| Obrigatória | Composição do NP_score | `figures/phase9/np-score-composition.svg` | Explica componentes, pesos, penalização de qualidade e classificação | Pronta |
| Obrigatória | Processo de validação | `figures/phase9/validation-workflow.svg` | Mostra a sequência entre congelamento, qualidade, reconstrução, baselines e claims | Pronta |
| Obrigatória | Distribuição por classe | `np-score-distribution.svg` já produzido pela Fase 9 | Eventos tendem a apresentar scores superiores aos controlos | Pronta |
| Obrigatória | Comparação ROC | `roc-comparison.svg` já produzido pela Fase 9 | Compara capacidade de ordenação entre modelos | Pronta |
| Obrigatória | Trade-off dos limiares | `threshold-tradeoff.svg` já produzido pela Fase 9 | Evidencia que o limiar 0,80 não é atingido nesta reconstrução | Pronta |
| Recomendada | Estabilidade temporal | `figures/phase9/temporal-validation-comparison.svg` | Compara exploração 2017–2022 com holdout 2023–2024 | Pronta, com caveat de amostra pequena |
| Recomendada | Escada evidência–afirmação | `figures/phase9/evidence-claim-ladder.svg` | Impede apresentar validação exploratória como calibração | Pronta |
| Recomendada | Desenho dos cenários A/B/C | `figures/phase9/scenario-evidence-design.svg` | Clarifica o que cada contraste deve demonstrar | Pronta; resultados dependem de runs |

## Figuras recomendadas para anexos ou discussão

| Figura | Ficheiro | Razão para não ocupar o corpo principal |
|---|---|---|
| Precision–Recall completa | `precision-recall-comparison.svg` | Importante com classe rara, mas pode acompanhar a ROC em anexo ou subfigura |
| Sensibilidade dos pesos | `sensitivity-stability.svg` | Sustenta robustez, mas contém detalhe metodológico elevado |
| Correlação entre componentes | `figures/phase9/component-correlation-ranking.svg` | Útil para discutir redundância e dupla contabilização |
| ERD completo | `erd-full.svg` | Demasiado denso para o corpo; manter o ERD simplificado no relatório |
| Inventário de claims por classe | `claims-by-evidence-class.svg` | Adequado para auditoria, não para explicar o modelo ao leitor geral |

## Figuras adicionais a produzir quando existirem runs atuais

1. **Série temporal sincronizada A/B/C**: BaseRisk, NP_score, confiança e integridade no mesmo eixo temporal.
2. **Latência p50/p95/p99 por cenário e escala**: barras agrupadas, nunca apenas média.
3. **Cobertura e mensagens em falta por ciclo**: linha ou heatmap ciclo × módulo.
4. **Matriz de degradação**: perfil de falha × efeito no score, confiança, integridade e elegibilidade.
5. **Mapa territorial**: células coloridas por suscetibilidade e, separadamente, por score diário; não misturar mapas estáticos e dinâmicos sem legenda explícita.
6. **Diagrama de sequência de uma run**: Simulator → RabbitMQ → Prevention → PostgreSQL/Influx → API → WebUI, com `SimulationRunId`, `CycleIndex` e `GridCellId`.
7. **Reconstrução de uma ocorrência**: timeline de mensagens e logs por módulo, mostrando como a UI permite explicar uma decisão.
8. **Escalabilidade**: células/processamento versus throughput, latência p95, CPU e memória.

## Tabelas que devem acompanhar as figuras

- definição operacional de cada métrica e respetivo denominador;
- comparação dos modelos com ROC-AUC, PR-AUC, IC95 e tamanho do efeito;
- matriz dos limiares com sensibilidade, especificidade, precisão e dias de alerta sem evento elegível por 30 dias;
- cobertura dos dados por ano e número de eventos positivos;
- defaults territoriais utilizados e respetivo impacto;
- resultados A/B/C com versão, seed, duração e número de células;
- limitações, consequência na interpretação e ação futura.

## Regras de apresentação

- Cada gráfico deve ter uma frase de conclusão imediatamente antes ou depois.
- As curvas ROC e PR devem indicar o número de positivos e negativos.
- Não apresentar FWI como validação independente sem explicar que integra a fórmula.
- Não usar o NP_score como percentagem ou probabilidade.
- Não usar apenas médias para latência; incluir percentis e dispersão.
- Não usar gráficos de barras com eixo truncado.
- Em resultados anuais sem eventos, apresentar `não estimável`, nunca zero.
- Distinguir visualmente `resultado observado`, `resultado reconstruído` e `resultado ainda não recolhido`.
- Manter SVG como formato principal no LaTeX quando a toolchain o suportar; conservar PNG como fallback.

## Sequência narrativa recomendada

1. O que o NP_score mede e como é composto.
2. Como a validação foi desenhada e que dados foram usados.
3. Qualidade e limitações dos dados.
4. Separação entre eventos e controlos.
5. Comparação com baselines.
6. Escolha e falha dos limiares atuais.
7. Estabilidade temporal e sensibilidade.
8. Resultados operacionais A/B/C.
9. Associação D0 versus indícios D-1/D-2.
10. Limites das conclusões e percurso para calibração futura.


## Atualização Fase 12

- Usar apenas figuras regeneradas depois da correção da Average Precision e da cobertura de eventos até 2024.
- Acrescentar `figures/phase12/lag-discrimination.svg` no corpo ou na discussão para separar associação D0 de antecipação D-1/D-2.
- Acrescentar `figures/phase12/event-source-specificity.svg` em anexo para mostrar que a maioria dos positivos provém de seeds regionais.
- Não reutilizar gráficos anteriores que incluam 2025 como classe negativa.
