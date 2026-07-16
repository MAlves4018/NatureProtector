# Governação, proveniência e rastreabilidade da evidência

## Objetivo

A Fase 10 estabelece uma camada transversal sobre os coletores existentes. Não substitui as Fases 1–9 e não cria resultados de runtime. O seu objetivo é responder, de forma automática, às seguintes perguntas:

- que ficheiros existem em cada campanha;
- de que fase e run veio cada artefacto;
- se o conteúdo continua igual ao que foi hashado;
- que afirmações do relatório possuem uma fonte rastreável;
- que figuras e tabelas estão prontas para integração;
- que evidências continuam em falta;
- o que mudou entre duas campanhas.

## Unidade canónica

A unidade canónica de evidência é o conjunto:

`BaselineId + RunId + Phase + ArtifactPath + SHA256`

Um número não deve ser transportado para o relatório sem manter, direta ou indiretamente, estes identificadores. O `SimulationRunId` é acrescentado quando a evidência resulta de uma execução do simulador.

## Classes de evidência

A Fase 10 preserva as classes já usadas pelo harness:

- `CURRENT_EXECUTION`;
- `CURRENT_STATIC_VERIFICATION`;
- `CURRENT_ANALYTICAL_EVIDENCE`;
- `HISTORICAL_EXECUTION`;
- `IMPLEMENTED_NOT_EXECUTED`;
- `BLOCKED_OR_PENDING`;
- `NO_SOURCE_EVIDENCE`.

A classe não é inferida a partir do aspeto de um gráfico. É herdada da fase produtora e do claim register.

## Cadeia de proveniência

A cadeia recomendada é:

`fonte → coletor → dataset intermédio → cálculo/verificador → tabela ou figura → claim → capítulo`

Cada ligação deve ser recuperável através de caminhos relativos, manifests e metadados. A Fase 10 gera `claim-lineage.*` e um diagrama para tornar esta cadeia auditável.

## Integridade

Os `SHA256SUMS.txt` são verificados sem reescrever os manifests das fases anteriores. Um mismatch é bloqueante, pois significa que o conteúdo observado já não corresponde ao conteúdo originalmente registado.

Ficheiros não cobertos por um manifesto são classificados como `NOT_MANIFESTED`; isto não prova corrupção, mas reduz a força da proveniência.

## Separação de runs

Nunca se deve copiar seletivamente o melhor gráfico de uma run e a melhor métrica de outra para formar uma campanha aparente. Quando duas campanhas são comparadas, deve ser usado `compare-evidence-campaigns.py`, mantendo os lados `left` e `right` explícitos.

## Capturas manuais

Screenshots, exportações da WebUI e imagens do Grafana são registadas através de `register-evidence-capture.py`. A imagem é copiada para um diretório imutável, recebe SHA-256 e um sidecar `metadata.json` com contexto, filtros, cenário, run e limitações.

## Limite de afirmação

A Fase 10 avalia qualidade e prontidão da evidência. Um resultado `READY_TO_SHARE` não significa que o sistema, o NP_score ou a hipótese científica estejam validados; significa apenas que o pacote de evidência disponível é íntegro, rastreável, suficientemente completo e apresentável segundo as regras configuradas.
