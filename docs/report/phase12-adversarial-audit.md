# Auditoria adversarial da recolha de evidência e validação do NP_score — Fase 12

## Veredito

A arquitetura atual **faz sentido e funciona como pipeline estático, analítico e documental**, mas ainda não constitui validação operacional completa do NatureProtector nem calibração científica do NP_score.

Foram confirmados:

- coerência dos parâmetros Candidate V1 entre configuração e constantes C#;
- reconstrução determinística dos 3 287 dias meteorológicos disponíveis;
- exclusão explícita de datas sem cobertura conhecida da fonte de incêndios;
- análise discriminativa reproduzível com 1 472 dias sazonais elegíveis e 23 datas de evento;
- funcionamento dos coletores, verificadores, manifests SHA-256 e integração no pacote do relatório;
- preservação das limitações e lacunas em vez da criação de valores sintéticos.

Não foram confirmados neste ambiente:

- paridade integral Python–C# por vetores dourados executados no runtime .NET;
- execução atual dos testes backend;
- pipeline integrado atual com RabbitMQ, PostgreSQL, InfluxDB, API e WebUI;
- benchmarks atuais de escalabilidade;
- campanha atual de fiabilidade e degradações;
- validade prospetiva ou calibração probabilística do NP_score.

## Problemas materiais encontrados e corrigidos

### 1. Average Precision incorreta quando existiam scores empatados

A implementação anterior percorria observações empatadas uma a uma. A ordem interna dos rótulos dentro de cada empate podia alterar a curva Precision–Recall e inflacionar a Average Precision, sobretudo nos modelos com scores por patamares.

**Correção:** as observações são agora agregadas por threshold único antes da construção da curva. A implementação foi comparada de forma independente com `scikit-learn` e apresentou os mesmos valores para todos os modelos testados.

**Impacto:** algumas baselines simples tinham resultados anteriores artificialmente elevados. Os resultados corrigidos devem substituir as tabelas e figuras anteriores.

### 2. O ano de 2025 era tratado como negativo sem cobertura da fonte de eventos

A meteorologia cobre 2017–2025, mas a fonte de incêndios configurada termina em 31 de dezembro de 2024. Os 184 dias sazonais de 2025 não podem ser classificados como dias sem evento.

**Correção:** essas datas são mantidas no dataset diário, mas recebem rótulo vazio e são excluídas das métricas discriminativas.

**População corrigida:** 1 472 dias sazonais entre 2017 e 2024, dos quais 23 são datas positivas.

### 3. Associação no próprio dia podia ser interpretada como previsão

O score D0 usa variáveis meteorológicas do próprio dia e os eventos possuem essencialmente resolução diária. Esta análise demonstra associação retrospetiva concorrente, não antecipação operacional.

**Correção:** foram acrescentadas avaliações D-1 e D-2, em que o score é obtido um e dois dias antes da data do evento.

| Disponibilidade do score | ROC-AUC | Average Precision | Interpretação |
|---|---:|---:|---|
| D0 | 0,879 | 0,076 | Associação retrospetiva no próprio dia |
| D-1 | 0,860 | 0,108 | Indício preliminar de antecipação a um dia |
| D-2 | 0,828 | 0,067 | Indício preliminar de antecipação a dois dias |

Os valores D-1 e D-2 continuam a não ser validação prospetiva, porque a fórmula e o desenho da análise foram observados no mesmo projeto e a amostra positiva é pequena.

### 4. Proveniência espacial dos eventos era demasiado ampla

A maioria das datas positivas deriva de progressões ou seeds em municípios próximos, e não de ignições confirmadas dentro da célula ou município analisado.

**Correção:** a avaliação passa a ser estratificada por tipo de fonte. Apenas quatro datas sazonais correspondem à interseção municipal/ICNF mais direta; 21 correspondem a seeds de progressão em municípios próximos.

**Consequência:** os resultados suportam associação regional com perigo, não previsão local de ignição.

### 5. O território estático não pode demonstrar valor acrescentado temporal

O mesmo perfil territorial agregado é aplicado a todos os dias. Desta forma, o território altera o nível absoluto do score, mas não a ordenação temporal.

Resultados do diagnóstico:

- Spearman entre NP_score completo e NP_score sem território: **1,000**;
- diferença de ROC-AUC: **0,000**.

Isto não prova que o território seja inútil. Prova apenas que o dataset atual, com um único perfil territorial estático, não consegue medir o seu valor. São necessários rótulos por célula, meteorologia por célula e várias áreas.

### 6. Baselines retrospetivas podiam usar informação de todo o período

Algumas baselines usavam percentis calculados sobre o período completo. Isso seria inadequado para uma afirmação prospetiva.

**Correção:** foram adicionadas baselines cujas transformações são ajustadas apenas em 2017–2022 e aplicadas sem reajuste ao holdout 2023–2024.

No holdout, a baseline meteorológica simples continua superior ao NP_score, pelo que a conclusão não resulta apenas de leakage dos percentis. Contudo, o holdout contém apenas sete datas positivas.

### 7. Teste Mann–Whitney não corrigia empates

A aproximação normal anterior ignorava empates, comuns em componentes por patamares.

**Correção:** a variância usa agora correção de empates e continuidade. O resultado continua a ser apresentado como aproximado e acompanhado do tamanho do efeito.

### 8. O score de governação podia parecer percentagem de evidência recolhida

Uma pontuação elevada de integridade, rastreabilidade e apresentação não significa que todas as campanhas operacionais tenham sido executadas.

**Correção:** os outputs distinguem agora:

- `governanceQualityScore`;
- `evidenceCoveragePercent`;
- `selectedPhaseCompletionPercent`;
- `potentialCoverageAfterExecutionPercent`.

O estado `PLAN_READY_EVIDENCE_INCOMPLETE` substitui formulações que podiam sugerir fecho efetivo quando existia apenas um plano completo.

### 9. Evidência histórica B/C precisava de melhor reconciliação

Os resumos históricos eram úteis, mas não continham os diretórios integrais das runs.

**Correção:** os extratos SQL passam a ser reconciliados com o JSON e os manifests quanto a UUID, cenário, inbox, avaliações, rejeitados, quarentena e sensores. A afirmação permanece limitada a **evidência histórica resumida**, não a uma run atual integralmente reproduzível.

### 10. Diagramas e gráficos podiam induzir leitura incompleta

Foram corrigidos:

- fundo branco explícito nos SVG/PNG;
- eixos ROC e PR limitados a [0,1];
- baseline de prevalência na curva PR;
- legendas com amostras de linha;
- rótulos do histograma;
- expressão “dias de alerta sem evento elegível” em vez de “falsos alarmes”;
- diagrama da fórmula, incluindo agregação de área `70% P80 + 30% máximo`.

## Resultados corrigidos do Candidate V1

| Métrica | Resultado |
|---|---:|
| Dias meteorológicos reconstruídos | 3 287 |
| Dias sazonais elegíveis | 1 472 |
| Dias sazonais fora da cobertura de eventos | 184 |
| Datas positivas | 23 |
| ROC-AUC do NP_score | 0,879 |
| IC95 bootstrap da ROC-AUC | [0,838; 0,924] |
| Average Precision | 0,076 |
| Sensibilidade no limiar 0,60 | 0,783 |
| Sensibilidade no limiar 0,80 | 0,000 |
| Máximo histórico reconstruído | 0,774 |
| Correlação com extensão ardida | ρ = 0,065 |

A baseline principal ajustada apenas no período de treino obteve ROC-AUC 0,889 e Average Precision 0,121. Assim, a complexidade atual do NP_score **ainda não demonstra melhoria relativamente a uma baseline meteorológica simples**.

## O que funciona atualmente

### Confirmado neste ambiente

- coletores e verificadores Python;
- contratos e schemas usados pelas Fases 9–11;
- geração de CSV, JSON, Markdown e SVG;
- integração de resultados da Fase 9 na Fase 7;
- indexação e manifests SHA-256;
- deteção explícita de fases ausentes;
- testes unitários do harness;
- verificação de sintaxe Python e shell;
- execução formal isolada da Fase 9 com 500 iterações bootstrap;
- execução integrada estática com perfil rápido.

### Faz sentido, mas ainda requer prova operacional

- pesos Candidate V1 como hipótese interpretável;
- confiança e integridade como dimensões separadas do perigo físico;
- comparação A/B/C;
- uso do território para discriminação espacial;
- limiares de aviso/alarme;
- capacidade de antecipação operacional;
- escalabilidade e fiabilidade da cadeia completa.

## Confirmação da ordem das fases

Foi inicialmente investigada a possibilidade de a Fase 11 estar incorretamente posicionada antes da Fase 7. A inspeção dos contratos mostrou que esta suspeita **não se confirma**: a Fase 11 não consome outputs da Fase 7; produz o estado de fecho de lacunas que a Fase 7 integra no pacote documental. A ordem atual é intencional:

```text
Fases de recolha → Fase 9 → Fase 11 → Fase 7 → Fase 10
```

A Fase 10 deve ser executada depois da campanha para indexar o pacote final.

## Limitações remanescentes

1. A paridade integral Python–C# ainda não foi executada.
2. A meteorologia provém de reanálise/modelo num ponto de referência, não de observações IPMA por célula.
3. Os positivos são raros e espacialmente heterogéneos.
4. Não existem negativos confirmados; existe apenas ausência de evento elegível conhecido.
5. O período de holdout contém sete positivos.
6. O território usa altitude por defeito nas 467 células e perigosidade por defeito em duas.
7. O limiar 0,80 não é atingido na reconstrução histórica atual.
8. Não existem runs atuais completas para backend, runtime, performance e fiabilidade neste ambiente.
9. O bootstrap formal deve ser usado na análise final; a campanha integrada pode usar perfil rápido para iteração.

## Decisão recomendada

O pacote está **pronto para integração com ressalvas**, desde que:

- as tabelas e figuras anteriores sejam substituídas pelos resultados corrigidos;
- o relatório use “validação retrospetiva exploratória” e nunca “probabilidade calibrada”;
- seja executada no ambiente local a campanha .NET/Docker definida nos runbooks;
- sejam preservados o manifesto, os hashes e o teto de afirmação;
- nenhuma lacuna seja marcada como concluída apenas porque existe um plano para a executar.
