# Norma de apresentação de evidência no relatório

## Regra claim–evidência–interpretação

Cada figura ou tabela no corpo do relatório deve ser acompanhada por três elementos:

1. **Claim:** o resultado que o leitor deve retirar;
2. **Evidência:** a métrica, distribuição ou observação visível;
3. **Interpretação:** por que é relevante e que limitações alteram a leitura.

A legenda não substitui a interpretação adjacente.

## Informação mínima de uma figura

- título neutro;
- população ou cenário;
- período temporal;
- unidade;
- tamanho da amostra quando relevante;
- baseline ou referência de comparação;
- indicação de IC95 quando calculado;
- source id ou referência ao claim register;
- nota de limitação quando muda a conclusão.

## Hierarquia visual recomendada para o Capítulo 6

### Corpo principal

1. desenho experimental e composição do NP_score;
2. qualidade e cobertura dos dados;
3. distribuição do score entre eventos e controlos;
4. comparação com baselines;
5. desempenho dos limiares;
6. estabilidade temporal ou sensibilidade;
7. comparação A/B/C;
8. desempenho operacional e escalabilidade;
9. scorecard final de evidência.

### Anexos

- curvas completas;
- matrizes por perfil de degradação;
- resultados anuais;
- tabelas de todas as runs;
- logs e manifests;
- screenshots suplementares;
- detalhes de ablação e bootstrap.

## Escolha de gráfico

- distribuição: boxplot, violin ou ECDF;
- ranking de modelos: barras horizontais com IC;
- trade-off de limiares: linha ou tabela de decisão;
- tempo: série temporal com eventos marcados;
- correlação: scatter com transparência, nunca apenas coeficiente;
- A/B/C: small multiples com escalas iguais;
- latência: p50/p95/p99 e distribuição, não apenas média;
- escalabilidade: throughput e latência por carga;
- cobertura: heatmap por ciclo e célula;
- proveniência: diagrama claim → fonte.

## Regras contra gráficos enganadores

- não truncar eixos de barras;
- não usar dois eixos Y sem justificação forte;
- não comparar cenários com escalas diferentes;
- não esconder falhas ou runs excluídas;
- não usar ROC-AUC isoladamente para eventos raros;
- não chamar “precisão” a accuracy;
- não apresentar correlação como causalidade;
- não usar médias sem distribuição ou percentis quando existem caudas longas;
- não misturar resultados exploratórios e holdout sem identificação.

## Consistência

As figuras devem usar a mesma nomenclatura do texto: `NP_score`, `BaseRisk`, confiança, integridade, cobertura, cenário e `SimulationRunId`. O mesmo limiar deve ter a mesma descrição em todas as tabelas e gráficos.
