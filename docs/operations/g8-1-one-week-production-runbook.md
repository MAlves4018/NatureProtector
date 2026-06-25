# Runbook G8.1 — Operação e teardown de uma semana

## Objetivo

Executar uma janela representativa de aproximadamente sete dias, provar CD, resiliência e recuperação e remover a infraestrutura após preservar evidence. O resultado pode sustentar `PRODUCTION_READY_AND_ONE_WEEK_RUNTIME_QUALIFIED`, nunca `LONG_TERM_PRODUCTION_PROVEN`.

## Plano

| Período | Atividade |
|---|---|
| Dia 0 | bootstrap platform, staging, secrets, DNS, deploy inicial |
| Dia 1 | primeira produção, smoke, baseline SLO/custo |
| Dias 1–3 | load, backlog, failover, PITR, rotações, incident drill |
| Dias 2–5 | soak contínuo >= 72 horas |
| Dias 5–7 | nova release, canary, rollback e segundo operador |
| Dia 7 | export, restore check, teardown, residual scan |

## Monitorização contínua

- disponibilidade e latência API/frontend;
- `429` por policy;
- instâncias, concorrência e saturação Cloud Run;
- queue depth, oldest message age e redeliveries;
- processing lag e throughput Prevention;
- ligações, locks, slow queries, backup e failover Cloud SQL;
- pod/quorum health;
- error-budget burn;
- custo diário e projeção mensal.

## Drills obrigatórios

- restart API;
- restart Prevention durante processamento;
- escala e backlog drain;
- perda de pod RabbitMQ;
- failover Cloud SQL;
- restore PITR isolado;
- rotação de password;
- rotação de certificados;
- release defeituosa e rollback;
- teardown e reconstrução por IaC.

Cada drill precisa de ID, timestamps, operador, release, expected outcome, observed outcome, RTO/RPO e evidence associada.

## Teardown

Antes de remover qualquer recurso:

1. congelar deployments;
2. exportar release manifest, rollouts, runtime summary e checksums;
3. validar backup e restore;
4. preservar logs/audit/evidence no bucket platform;
5. gerar estimativa mensal a partir dos sete dias;
6. desativar deletion protection de forma deliberada;
7. aplicar `create_edge=false` e `create_data_plane=false`;
8. procurar recursos residuais;
9. preservar teardown receipt;
10. manter apenas a fundação/evidence se explicitamente aprovado.

A ausência de qualquer ficheiro obrigatório faz o script falhar antes da destruição.
