# Metodologia e limites da validação do NP_score

## Pergunta de investigação

A análise procura responder a três perguntas distintas:

1. A implementação Candidate V1 é reproduzível e matematicamente coerente?
2. O índice tende a atribuir valores superiores a datas historicamente associadas ao início de incêndios?
3. A complexidade adicionada por FWI, KBDI e território melhora o resultado face a baselines mais simples?

A campanha não tenta demonstrar causalidade nem eficácia operacional.

## Unidade de análise

A unidade principal é o **dia local na área de Proença-a-Nova**, entre 2017 e 2025. Contudo, a classificação por evento termina em 2024; 2025 permanece sem rótulo e fora das métricas discriminativas. A análise discriminativa principal é limitada aos meses de maio a outubro para reduzir o efeito trivial da sazonalidade.

Uma data positiva corresponde à existência de pelo menos um registo histórico elegível com `start_date` nessa data. Uma data negativa significa apenas que não existe um registo elegível conhecido dentro do período coberto pela fonte; não constitui prova de ausência absoluta de incêndio. Datas fora da cobertura ficam sem rótulo.

## Evitar leakage e seleção favorável

- A fórmula Candidate V1 não é alterada durante a campanha.
- Os controlos são selecionados sem usar o valor do NP_score.
- O holdout 2023–2024 é reportado separadamente; contém apenas sete datas positivas.
- FWI e KBDI são tratados como baselines internos/relacionados, não como gold standards independentes.
- O cenário B não é usado isoladamente para “provar” a fórmula, porque foi selecionado por representar risco elevado.

## Métricas

### ROC-AUC

Mede a probabilidade de um evento escolhido aleatoriamente receber score superior a um controlo escolhido aleatoriamente. É uma métrica de ranking e não de calibração.

### Average precision

Resume a curva Precision–Recall. É especialmente relevante porque as datas positivas são raras.

### Mann–Whitney e Cliff's delta

Avaliam separação das distribuições sem assumir normalidade. O p-value é aproximado e deve ser acompanhado do tamanho do efeito e do número de eventos.

### Intervalos bootstrap

A reamostragem é estratificada por classe para impedir iterações sem eventos. O seed e o número de iterações ficam registados em `provenance.json`.

### Limiar

Os limiares são avaliados como regras operacionais e não escolhidos retroativamente como “melhores”. A tabela apresenta simultaneamente sensibilidade, especificidade, precisão e carga de falsos alertas.

## Limitações conhecidas

- pequena classe positiva e apenas sete positivos no holdout 2023–2024;
- mistura de fontes históricas e possíveis duplicações conceptuais;
- dados meteorológicos de referência, não necessariamente observações na ignição;
- território estático e comum a todos os dias;
- ausência de hora/local exatos para todos os eventos;
- ausência de informação completa de supressão e resposta;
- potencial double counting de condições meteorológicas no score e no FWI;
- extensão ardida não é um outcome adequado para validar perigo pré-ignição sem variáveis pós-ignição.

## Evolução para calibração científica

Uma fase futura necessitaria de eventos georreferenciados, negativos amostrados de forma explícita, dados horários, separação espacial treino/teste, validação temporal externa, modelos de calibração e comparação com índices operacionais nacionais. Os pesos candidatos só deveriam ser substituídos depois de uma comparação pré-registada e reproduzível.


## Auditoria adversarial da Fase 12

A Fase 12 corrigiu o tratamento de scores empatados na Average Precision e no Mann–Whitney, excluiu 2025 das métricas de evento, acrescentou análise D-1/D-2, estratificação por proveniência do evento, baselines ajustadas apenas no período de treino e um diagnóstico explícito do valor territorial. A implementação Python reproduz as constantes C#, mas a paridade integral entre linguagens permanece por executar com vetores dourados no runtime .NET.
