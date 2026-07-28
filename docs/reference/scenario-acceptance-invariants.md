---
id: NP-REF-SCENARIO-INVARIANTS
status: CURRENT
owner: Miguel Alves
audience: engineering, QA
source_of_truth: Simulator.Host degradation implementation, pipeline contracts and runtime diagnostics
last_verified_against: NatureProtector Phase 3 profile verifier
last_verified_at: 2026-07-22
review_triggers: simulator profile, pipeline, projection, audit or diagnostic changes
---

# Invariantes de aceitação por cenário

Este documento define o comportamento observável que a campanha automática deve provar. As regras são deliberadamente mais fortes do que “o processo terminou”. O ficheiro machine-readable de referência é [`config/acceptance/scenario-invariants.json`](../../config/acceptance/scenario-invariants.json). A campanha executável usa ainda os thresholds e runs suplementares versionados em [`config/acceptance/p0-runtime-coverage.json`](../../config/acceptance/p0-runtime-coverage.json), aplicados por `verify_scenario_profile_matrix.py`.

## Convenções comuns

Para uma run com `S` sensores resolvidos e `C` ciclos:

```text
expectedObservations = S × C
```

Salvo indicação contrária, uma run aceite deve terminar com:

- operação e run em estado terminal de sucesso;
- `Accounting.Settled = true`;
- `pendingInbox = processingInbox = retryPendingInbox = 0`;
- zero itens órfãos em processamento;
- ausência de processo `NatureProtector.Simulator.Host` após o fecho;
- correlação consistente entre request, operation e simulation run;
- evidence associada à identidade da execução atual quando `collectEvidence=true`.

Os valores concretos devem ser calculados a partir do pedido/resolução da run, nunca copiados de uma campanha histórica.

## Perfis

| Perfil | Invariantes específicos |
| --- | --- |
| `none` | `acceptedObservations = expectedObservations`; uma avaliação de risco por leitura aceite/elegível; zero missing, rejected e quarantined atribuíveis à run. |
| `missing-readings` | `0 < acceptedObservations < expectedObservations`; `missing = expected - accepted`; nenhuma leitura omitida cria assessment; repetição com a mesma seed e catálogo produz o mesmo padrão de omissão. |
| `noise` | total aceite mantém-se nominal; mesma seed é reproduzível; valores diferem da baseline `none` e mostram perturbação superior ao ruído base, sem sair dos limites físicos absolutos. |
| `bias` | total aceite nominal; delta assinado face à baseline é sistemático por métrica (temperatura positivo, humidade negativo, vento positivo); mesma seed reproduz o mesmo delta. |
| `drift` | total aceite nominal; diferença face à baseline cresce com o índice de ciclo na direção definida por métrica; não pode ser reduzida a um offset constante. |
| `stuck-value` | total aceite nominal; para pelo menos um sensor, o mesmo valor observado repete-se em vários ciclos apesar da evolução do truth snapshot; IDs de evento continuam únicos. |
| `outlier` | total aceite nominal; a injeção ocorre apenas no subconjunto determinístico definido pelo código; existem deltas materialmente superiores à baseline; classificação/eligibilidade persistida permanece coerente com o valor resultante. |
| `clipping/range` | total aceite nominal; valores respeitam os caps do perfil e os limites físicos finais; deve existir evidence de clipping face à baseline, não apenas valores válidos. |
| `lag/delay` | total aceite nominal; `IngestTime - EventTime` representa o atraso configurado dentro de tolerância; duração/diagnóstico evidencia atraso e a freshness resultante é coerente. |
| `duplicate` | a entrega inclui pelo menos uma repetição do mesmo envelope; persistência idempotente mantém no máximo uma leitura/assessment por EventId; não existe segundo efeito operacional. |
| `out-of-order` | total aceite nominal; ordem de entrega difere da ordem produzida; ciclos e snapshots convergem sem perda, dupla contagem ou regressão do estado terminal. |
| `retry-transient` | existe pelo menos uma tentativa transitória falhada/retry; o mesmo evento termina processado; uma única avaliação é persistida; o caso injetado não termina em quarentena. |


## Implementação automática atual

O estágio `p0-runtime-coverage`, selecionado por `Functional` e `Full`, executa os 12 perfis com uma seed fixa e persiste por caso o audit, accounting, timings, leituras aceites, inbox, tentativas, observações de ciclo e ordem real de publicação. A matriz repete `missing-readings` com a mesma seed e adiciona `outlier + clipping/range` para provar saturação, evitando um falso positivo em que todos os valores apenas permanecem dentro dos limites.

## Comparações B/C

A comparação canónica deve executar:

- `scenario_b` com `none`;
- `scenario_c` com `missing-readings`;
- mesma dimensão (`sensorCount`, `numberOfCycles`, `intervalSeconds`) e seeds registadas;
- `compare-latest-b-vs-c` apenas depois de ambas as runs estarem settled.

Critérios:

- B apresenta total nominal;
- C apresenta redução determinística de accepted e aumento correspondente de missing;
- nenhuma das duas depende de dados de uma run anterior;
- IDs e timestamps usados na comparação pertencem à campanha atual.

## Casos negativos P3

A campanha P3 deve provar, por EventId/correlation ID, pelo menos:

| Caso | Destino esperado | Assessment permitido? |
| --- | --- | --- |
| JSON inválido | rejected antes da inbox processável | não |
| payload ausente | rejected | não |
| event type não suportado | rejected | não |
| schema version não suportada | rejected | não |
| operational state inválido | rejected | não |
| sensor inexistente | quarantined | não |
| mesmo EventId com payload divergente | erro `duplicate_payload_mismatch`/quarantine | não para o payload divergente |
| falha transitória seguida de sucesso | retry e processed | exatamente um |
| retries esgotados | quarantined | não |
| falha permanente | quarantined | não |

A disponibilidade P3 e o pedido HTTP não são suficientes: as tabelas `pipeline.*`, `projection.*` e o query pack/audit devem confirmar cada resultado.

## Tolerâncias

Tolerâncias temporais e numéricas deverão ser declaradas no runner, guardadas no `run-spec.json` e nunca ampliadas silenciosamente depois de uma falha. Alterações justificadas exigem atualização deste contrato e dos testes correspondentes.
