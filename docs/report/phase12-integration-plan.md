# Plano de integração — Fase 12

## Objetivo

Integrar as correções adversariais da validação do NP_score e da governação de evidência sem alterar a fórmula de produção Candidate V1, pesos, limiares ou serviços runtime.

## Estratégia

1. Criar uma branch isolada a partir da branch de integração atual.
2. Confirmar que não existem campanhas a escrever em `artifacts/report-evidence`.
3. Aplicar o patch da Fase 12 ou copiar o overlay mantendo a estrutura relativa.
4. Fazer merge semântico nos ficheiros modificados; não substituir alterações posteriores do repositório.
5. Executar os 37 testes do harness.
6. Executar a Fase 9 formal com 500 bootstraps.
7. Executar a campanha integrada com o perfil rápido, usando 100 bootstraps.
8. Executar a Fase 10 sobre a campanha terminada.
9. Executar os runbooks locais de .NET/Docker para fechar backend, runtime, performance e fiabilidade.
10. Só depois atualizar o Capítulo 6 e promover claims operacionais.

## Comandos de validação estática

A partir da raiz do repositório:

```bash
python -m unittest discover -s tests/evidence -p 'test_*.py'
python -m compileall -q scripts/evidence tests/evidence
bash -n scripts/evidence/*.sh
```

## Execução formal isolada da Fase 9

```bash
python scripts/evidence/collect-np-score-validation.py \
  --repo . \
  --baseline-id phase12-formal \
  --run-id phase12-formal-001 \
  --config config/evidence/np-score-validation.json \
  --output artifacts/report-evidence/phase12-formal/09-np-score-validation/phase12-formal-001 \
  --bootstrap-iterations 500 \
  --overwrite

python scripts/evidence/verify-np-score-validation.py \
  artifacts/report-evidence/phase12-formal/09-np-score-validation/phase12-formal-001 \
  --require-complete
```

## Campanha integrada rápida

Usar o perfil estático/quality com 100 iterações durante desenvolvimento. Esta execução valida contratos, integração e formato; não substitui a execução formal das métricas.

## Recolha operacional local obrigatória

Executar num computador com:

- .NET SDK compatível;
- Docker Desktop/Engine;
- PowerShell 7;
- PostgreSQL, RabbitMQ e InfluxDB através da composição aprovada.

Recolher, pela ordem:

1. testes e cobertura backend;
2. runtime integrado A/B/C;
3. benchmarks e escalabilidade;
4. fiabilidade/degradações;
5. nova Fase 9 importando as evidências runtime;
6. Fase 7;
7. Fase 10.

## Ficheiros com merge semântico obrigatório

- configurações em `config/evidence/`;
- orquestrador e verificadores da campanha;
- coletor de integração do relatório;
- documentação ativa de recolha;
- catálogo visual e metodologia do NP_score.

## Critérios de aceitação

- 37/37 testes do harness aprovados;
- Fase 9 formal verificada;
- datas de 2025 fora das métricas de evento;
- AP idêntica a uma implementação independente em casos com empates;
- estados `PLAN_READY_EVIDENCE_INCOMPLETE` preservados quando faltam runs;
- manifests SHA-256 válidos;
- Fase 7 não promove resultados de fases ausentes;
- Fase 10 distingue qualidade de governação de cobertura efetiva;
- relatório atualizado com os valores corrigidos e respetivas ressalvas.

## Rollback

A Fase 12 não altera a fórmula ou runtime. O rollback consiste em reverter o commit único da fase. Não remover evidências antigas; mantê-las identificadas como superseded para preservar a cadeia de auditoria.
