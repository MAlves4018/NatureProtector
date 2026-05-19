# Presentation dry run, progresso 2026-05-22

Objetivo: guiao curto para uma apresentacao de 8 a 10 minutos, usando apenas evidencias verificaveis desta pasta e evitando claims acima do que foi validado.

## Sequencia de 8-10 minutos

| Tempo | Slide | Conteudo | Evidencia a abrir se perguntarem |
|---:|---|---|---|
| 0:00-0:45 | 1. Estado do projeto | Dizer que a baseline tecnica esta operacional: simulador, RabbitMQ, Prevention Host, PostgreSQL, API/UI e evidence pack. | `01-git-status.txt`, `02-build.txt`, `03-tests.txt` |
| 0:45-1:45 | 2. Arquitetura atual | Explicar o fluxo Simulator Host -> RabbitMQ -> Prevention Host -> PostgreSQL -> Backoffice/API/UI. | Codigo do simulator/publisher e Runtime Monitor |
| 1:45-2:45 | 3. Cadeia metodologica V1 | Mostrar separacao `TruthSnapshot -> LocalObservation -> payload`. | `TruthSnapshot.cs`, `LocalObservation.cs`, testes do simulador |
| 2:45-4:15 | 4. Cenarios e degradacao | Mostrar B `none` 30/30 e C `missing-readings` 24/30 sem rejected/quarantine. | `04-scenario-b-summary.sql.txt`, `05-scenario-c-summary.sql.txt`, `06-compare-b-vs-c.json` |
| 4:15-5:15 | 5. Runtime Monitor e Grafana | Mostrar UI se estiver limpa; se nao, usar SQL/JSON como prova principal. | Runtime Monitor, Grafana, `07-runtime-notes.md` |
| 5:15-6:30 | 6. Requisitos obrigatorios | Explicar Feito/Parcial/Em curso, sem mascarar RBAC. | `08-requirements-status.md` |
| 6:30-7:45 | 7. Riscos | Dizer os riscos principais e mitigacao ate dia 1. | `10-risks-and-plan-to-demo.md`, `13-rbac-checkpoint.md` |
| 7:45-9:00 | 8. Plano ate dia 1 | Fechar com prioridades: smoke final, polish UI, RBAC minimo, profiles P1 simples se seguro. | `09-degradation-profiles-plan.md` |
| 9:00-10:00 | Perguntas | Responder com evidencia concreta, nao com promessas. | Pasta `docs/evidence/progress-2026-05-22/` |

## Perguntas provaveis e respostas

| Pergunta | Resposta curta | Evidencia |
|---|---|---|
| O scenario B esta mesmo limpo? | Sim. A run nova `d8203d4b-1839-4908-87ef-05633c1f1ae5` tem 30 expected, 30 inbox, 30 risk assessments, 0 rejected e 0 quarantined. | `04-scenario-b-summary.sql.txt` |
| O scenario C e um terceiro clima? | Nao. E uma degradacao operacional com `missing-readings`, mantendo o mesmo contexto fisico base. | `09-degradation-profiles-plan.md` |
| Porque C tem menos eventos? | O profile `missing-readings` omite observacoes depois da verdade fisica. Nesta run: 24 accepted, 6 missing. | `06-compare-b-vs-c.json` |
| Isto valida cientificamente o score? | Nao. O score V1 e candidate parameter set para demonstracao operacional. Validacao cientifica fica fora desta apresentacao. | `10-risks-and-plan-to-demo.md` |
| RBAC esta pronto? | Ainda nao deve ser apresentado como pronto. Esta marcado como Em curso com perguntas abertas ao colega. | `13-rbac-checkpoint.md` |
| A UI mostra tudo? | A UI/Runtime Monitor e util para demo, mas a evidencia tecnica principal vem de PostgreSQL/JSON. Pode precisar de polish. | `08-requirements-status.md` |
| O contrato RabbitMQ mudou? | Nao. A correcao foi interna ao simulador e nao alterou `EventEnvelope<SensorReadingProducedPayload>`. | Testes do simulador e publisher |

## Checklist pre-apresentacao

- [ ] Confirmar que Docker/PostgreSQL/RabbitMQ/Influx estao ativos.
- [ ] Abrir Runtime Monitor antes da apresentacao.
- [ ] Ter `06-compare-b-vs-c.json` aberto como backup.
- [ ] Ter `04-scenario-b-summary.sql.txt` e `05-scenario-c-summary.sql.txt` prontos.
- [ ] Nao usar runs antigas com B 27/30 como resultado final.
- [ ] Dizer explicitamente que RBAC esta em curso.
- [ ] Dizer explicitamente que score V1 nao e validacao cientifica.
- [ ] Fazer smoke final se houver tempo: scenario B `none` e scenario C `missing-readings`.

## Criterio de sucesso da apresentacao

- A audiencia entende que ha pipeline runtime ponta a ponta.
- A diferenca B vs C e clara e quantificada.
- Os riscos sao assumidos com plano curto ate dia 1.
- Nao ha claims sem evidencia concreta.
