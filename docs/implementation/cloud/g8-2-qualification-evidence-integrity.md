# G8.2 — Qualification and evidence integrity remediation

## Estado

`IMPLEMENTED_STATICALLY_AWAITING_RUNTIME_ADAPTERS_AND_OWNER_EXECUTION`

O G8.2 substitui a cadeia G8 que aceitava um resumo de qualificação preenchido fora da evidence e que criava uma dependência circular entre qualificação e arquivo. Não cria projetos GCP, não implanta produção e não altera contratos de eventos, scoring ou migrations existentes.

## Objetivos

- ligar toda a evidence ao mesmo `qualification_id`, commit e manifesto imutável;
- validar todos os documentos com JSON Schema Draft 2020-12;
- rejeitar ficheiros extra, em falta, symlinks e hashes divergentes;
- calcular métricas agregadas a partir de action records selados;
- validar workflow, run ID, branch, repositório, conclusão e SHA através da API GitHub;
- restringir attestations ao workflow, source digest e source ref esperados;
- separar pré-qualificação, arquivo e qualificação final;
- exigir revisão e autorização assinadas por identidades distintas;
- garantir que autorização não equivale a deployment.

## Cadeia canónica

```text
G8.1 immutable release
  -> G8.2 runtime probe
  -> attested probe bundle
  -> G8.2 action ingest
  -> strict action record
  -> attested action bundle
  -> deterministic aggregator
  -> closed-world evidence index
  -> PRE_ARCHIVE_QUALIFICATION_PASSED
  -> owner-managed immutable archive
  -> archive receipt
  -> FINAL_QUALIFICATION_PASSED
  -> independent signed review
  -> authorization request
  -> signed human authorization
  -> authorization verification
```

Nenhuma etapa G8.2 muda tráfego, executa `terraform apply`, aprova rollout ou implanta produção.

## Action records

Cada ação é produzida por um workflow separado e tem um run ID único. Os pilotos requerem execution IDs e `SimulationRunId` distintos em três datas UTC diferentes. O soak é reconstruído a partir de timestamps, e não de uma duração declarada. Disponibilidade, sucesso, percentis, perda e headroom são calculados pelo agregador.

Ações obrigatórias:

- `pilot-1`, `pilot-2`, `pilot-3`;
- `soak-start`, `soak-finish` e observações opcionais;
- `capacity`;
- `security-rotation`;
- `incident-drill`;
- `collect-audit`;
- `cost-observation`;
- `second-operator`;
- `rollback-drill`;
- `teardown-rehearsal`.

## Runtime probes

O workflow não aceita um JSON de medição nem um comando arbitrário como input. Cada ação resolve um adapter versionado em `scripts/cloud/probes/`. O adapter tem de recolher factos brutos de APIs/runtime e escrever `raw-probe-source.json`. `New-G82ProbeMeasurement.py` deriva a medição canónica e rejeita invariantes inválidas.

Os source adapters de runtime permanecem deliberadamente fail-closed até serem integrados e ajustados ao ambiente real depois do G10. A ausência de adapter não pode produzir PASS.

## Correção funcional da API

`RuntimeOrchestration:AllowRemoteLaunch` foi adicionado com valor predefinido `false`. Só pode ser ativado com `Mode=CloudRunJob`. O manifesto cloud ativa-o explicitamente. Isto permite que staging/produção iniciem o Simulator através de Cloud Run Jobs sem reativar processos locais.

## Custos e janela de execução

O plano G8.2 adapta o gate de custos à janela aprovada de aproximadamente uma semana:

- pelo menos sete dias observados;
- custo real da janela;
- projeção mensal calculada;
- orçamento mensal aprovado;
- sem claim de estabilidade de custo de 30 dias.

## Limitações

- cloud runtime não executado;
- source adapters de cada ação ainda precisam de integração com os outputs reais G8.1/G9;
- assinaturas OpenSSH reais não foram executadas neste ambiente;
- nenhuma autorização ou deployment foi emitido.
