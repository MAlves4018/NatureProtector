# Comparação entre campanhas e análise de regressões

## Objetivo

A comparação entre runs deve distinguir alterações reais de variação experimental. O utilitário `compare-evidence-campaigns.py` cria um diff estrutural de estados, claims, contagens e métricas do NP_score, mas não decide automaticamente se uma alteração é boa.

## Comparações válidas

Duas runs são diretamente comparáveis quando mantêm:

- mesmo commit ou alteração explicitamente identificada;
- mesma configuração;
- mesmo cenário e seed;
- mesma população de células;
- mesma duração ou critério de conclusão;
- mesmas versões de dependências relevantes;
- mesma infraestrutura ou perfil de recursos;
- mesmo método de recolha.

Quando algum destes elementos muda, deve aparecer como fator experimental.

## Métricas direcionais

### Menor é normalmente melhor

- latência p50, p95 e p99;
- tempo até convergência;
- mensagens em falta;
- duplicados não tratados;
- backlog;
- erros e retries;
- memória por célula.

### Maior é normalmente melhor

- cobertura;
- throughput dentro do SLO;
- avaliações elegíveis;
- rastreabilidade;
- sensibilidade com falsos alertas controlados;
- percentagem de claims com fonte íntegra.

### Sem direção universal

- NP_score médio;
- número de alertas;
- CPU utilizada;
- duração total;
- correlação;
- ROC-AUC sem considerar PR-AUC e prevalência.

## Regressão

Uma regressão deve ser declarada apenas quando:

1. a diferença excede o ruído ou intervalo esperado;
2. a comparação é metodologicamente válida;
3. a direção esperada está definida;
4. não existe explicação por mudança de população;
5. o resultado se repete ou é materialmente grande.

## Comando

```bash
python3 scripts/evidence/compare-evidence-campaigns.py \
  --left artifacts/report-evidence/<baseline-a> \
  --right artifacts/report-evidence/<baseline-b> \
  --output artifacts/report-evidence/comparisons/<a>-vs-<b>
```

O output contém JSON, CSV e Markdown. As métricas alteradas devem ser depois classificadas manualmente como melhoria, regressão, alteração esperada ou inconclusiva.
