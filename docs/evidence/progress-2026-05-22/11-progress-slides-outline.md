# Outline de slides, progresso 2026-05-22

## Slide 1. Estado do projeto

Mensagem principal: o projeto já executa uma cadeia runtime ponta a ponta para simulação, ingestão, processamento, risco e evidência.

Bullets:
- Runtime local com Simulator Host, RabbitMQ, Prevention Host, PostgreSQL, Backoffice API, webUI/Runtime Monitor e Influx/Grafana.
- Cadeia operacional com `SimulationRunId`, inbox durável, retry/quarantine e projeções.
- Foco da semana: estabilizar B limpo, C degradado e evidence pack para progresso.

Evidência/screenshot a usar:
- `docs/evidence/progress-2026-05-22/06-compare-b-vs-c.json`
- Runtime Monitor com as runs B/C recentes.

Speaker notes:
- Abrir com estado real, não prometer trabalho futuro como se já estivesse feito.
- Dizer que os outputs desta pasta são a fonte única para os slides.

## Slide 2. Arquitetura atual

Mensagem principal: a arquitetura já separa simulação, transporte, processamento e visualização.

Bullets:
- Simulator Host publica `EventEnvelope<SensorReadingProducedPayload>`.
- RabbitMQ transporta eventos; Prevention Host consome e grava inbox/attempts.
- PostgreSQL guarda control plane, pipeline, projections e risk assessment log.
- Backoffice/API/UI expõem estado operacional e runtime monitor.

Evidência/screenshot a usar:
- Diagrama simples do fluxo.
- `01-git-status.txt`, `02-build.txt`, `03-tests.txt` como evidência técnica.

Speaker notes:
- Explicar que não houve alteração no contrato RabbitMQ nesta estabilização.

## Slide 3. Cadeia metodológica V1

Mensagem principal: a cadeia V1 está representada no runtime, com separação explícita entre verdade física e observação.

Bullets:
- `TruthSnapshot`: verdade física simulada.
- `LocalObservation`: observação local com possibilidade de degradação.
- Payload externo permanece estável.
- Prevention transforma eventos em leituras normalizadas, elegibilidade, risco e projeções.

Evidência/screenshot a usar:
- `src/NatureProtector.Simulator.Host/Readings/TruthSnapshot.cs`
- `src/NatureProtector.Simulator.Host/Readings/LocalObservation.cs`
- Testes do simulador em `03-tests.txt`.

Speaker notes:
- Salientar que scenario C degrada observação/evento, não a verdade física.

## Slide 4. Cenários e degradação

Mensagem principal: B é baseline limpa; C é degradação controlada por `missing-readings`.

| Cenário | Profile | Expected | Accepted | Missing | Rejected | Quarantined |
|---|---|---:|---:|---:|---:|---:|
| scenario_b | none | 30 | 30 | 0 | 0 | 0 |
| scenario_c | missing-readings | 30 | 24 | 6 | 0 | 0 |

Bullets:
- B: `d8203d4b-1839-4908-87ef-05633c1f1ae5`.
- C: `36caca67-352c-41f1-80e3-8fe951a1582c`.
- Risk assessments acompanham os eventos aceites: B 30, C 24.

Evidência/screenshot a usar:
- `04-scenario-b-summary.sql.txt`
- `05-scenario-c-summary.sql.txt`
- `06-compare-b-vs-c.json`

Speaker notes:
- Esta é a tabela central da apresentação; não usar runs antigas.

## Slide 5. Runtime Monitor e Grafana

Mensagem principal: a demo deve mostrar estado operacional, não só logs.

Bullets:
- Runtime Monitor deve mostrar cenário, profile, contadores e estado de run.
- Grafana/Influx servem como apoio de observabilidade.
- Evidência técnica vem de PostgreSQL, não de screenshots isolados.

Evidência/screenshot a usar:
- Screenshot do Runtime Monitor com scenario B/C, se disponível.
- Painel Grafana com métricas de runtime, se estiver curado.
- `07-runtime-notes.md` para contextualização.

Speaker notes:
- Se a UI estiver menos polida, demonstrar a API/Runtime Monitor como prova e mostrar SQL/JSON como backup.

## Slide 6. Requisitos obrigatórios

Mensagem principal: a equipa sabe o que está feito, parcial e em curso.

Bullets:
- Configuração área/grelha, sensores, simulação, ingestão, persistência e risco estão com evidência.
- Alertas/UI/dashboards/RBAC/relatório ainda têm pontos parciais ou em curso.
- Extras úteis: Runtime Monitor, orquestrador de runs e comparação B/C.

Evidência/screenshot a usar:
- `08-requirements-status.md`

Speaker notes:
- Não mascarar RBAC como feito.
- Dizer claramente o que fica para dia 1.

## Slide 7. Riscos

Mensagem principal: os riscos estão identificados e têm mitigação.

Bullets:
- RBAC/roles incompleto.
- Demo precisa de smoke final.
- Profiles adicionais ainda limitados.
- Score V1 é candidate parameter set, não validação científica.
- UI/runtime monitor precisa polish.

Evidência/screenshot a usar:
- `10-risks-and-plan-to-demo.md`
- `13-rbac-checkpoint.md`

Speaker notes:
- Apresentar riscos como controlo de projeto, não como desculpa.

## Slide 8. Plano até dia 1

Mensagem principal: o caminho até à demo/design review está curto e priorizado.

Bullets:
- P0: repetir B/C, curar UI, fechar narrativa, confirmar RBAC mínimo.
- P1: implementar profiles simples se não ameaçarem estabilidade.
- Fora de âmbito: calibração científica, integração meteorológica externa, refactors grandes.

Evidência/screenshot a usar:
- `09-degradation-profiles-plan.md`
- `14-presentation-dry-run.md`

Speaker notes:
- Fechar com foco: estabilidade demonstrável primeiro, amplitude depois.
