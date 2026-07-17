# Fase 11 — fecho de lacunas e readiness gate

## Objetivo

A Fase 11 reduz as lacunas da campanha de evidências sem promover planos ou
configuração a resultados executados. A fase tem três responsabilidades:

1. admitir fontes históricas apenas depois de validar o schema, reconciliação,
   identificadores e proveniência;
2. distinguir evidência efetivamente presente de prontidão para a recolher;
3. produzir comandos, pré-requisitos e contratos de importação para as áreas
   que dependem de outro ambiente.

## Ordem na campanha

```text
Fases 1–6 → Fase 9 → Fase 11 → Fase 7 → Fase 10
```

A Fase 11 corre antes da integração do relatório para que a Fase 7 possa usar a
comparação histórica B/C admitida, quando não exista uma comparação runtime
atual. A Fase 10 corre depois e indexa também o pacote da Fase 11.

## Métricas separadas

- **Cobertura de evidência:** percentagem de requisitos que possuem uma fonte
  admitida e verificável.
- **Prontidão do fecho:** percentagem de requisitos já fechados ou que possuem
  comando, pré-requisitos e ação de fecho explícitos.
- **Cobertura potencial:** meta resultante da execução futura do plano. Nunca é
  apresentada como resultado alcançado.

## Admissão histórica B/C

A fonte histórica é aceite apenas quando:

- existem `scenario_b` e `scenario_c`;
- ambos os estados são `Completed`;
- os `SimulationRunId` são UUID válidos e distintos;
- `expectedEvents = inboxEvents + missingEvents`;
- as avaliações não excedem os eventos recebidos;
- o cenário B é nominal e sem eventos ausentes;
- o cenário C apresenta degradação controlada;
- os manifests correspondem ao cenário e ao número esperado de eventos;
- fonte e manifests são copiados para o pacote e protegidos por SHA-256.

A classificação resultante é `HISTORICAL_EXECUTION`. Não prova a execução do
snapshot atual nem substitui uma nova campanha A/B/C.

## Outputs principais

- `phase11-summary.json` e `.md`;
- `closure-matrix.json` e `.csv`;
- `historical-admission-audit.json`;
- `admitted/historical-runs.csv`;
- `environment-readiness.json`;
- runbooks Windows e Unix;
- checklist de fecho;
- tabela e figura de completude versus readiness;
- `SHA256SUMS.txt`.

## Execução

```powershell
& .\scripts\evidence\collect-evidence-gap-closure.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -Overwrite
```

Ou através da campanha canónica:

```powershell
& .\scripts\evidence\run-report-evidence-campaign.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -Profile quality `
  -Execute
```

## Teto de afirmação

A fase pode demonstrar que uma fonte histórica foi admitida, que uma área está
bloqueada por pré-requisitos concretos e que existe um procedimento reprodutível
para a fechar. Não pode converter uma run histórica em execução atual, nem um
runbook em benchmark, teste backend, campanha de fiabilidade ou validação
operacional.
