# Evidence README

## Objetivo

Centralizar evidências de implementação e validação para suportar decisões de aceite.

## Tipos de Evidência

| Tipo | Descrição | Exemplo |
|---|---|---|
| Execução de testes | Resultado objetivo de testes automáticos/manuais | Logs, outputs, screenshots |
| Verificação funcional | Prova de comportamento esperado | Passos + resultado observado |
| Compatibilidade de contratos | Confirmação de não regressão em integrações | Checklist de validação |
| Revisão técnica | Notas de revisão e riscos residuais | Sumário de revisão |

## Índice de Evidências (preencher)

| ID | Data | Tarefa | Tipo | Artefacto | Responsável | Observações |
|---|---|---|---|---|---|---|
| EVD-001 |  |  |  |  |  |  |
| EVD-002 |  |  |  |  |  |  |

## Regras

- Cada evidência deve referenciar uma tarefa específica.
- Evidência sem contexto (tarefa/critério) não deve ser aceite.
- Registar também riscos residuais identificados na validação.

## Contratos de estado cloud estáticos

- `g8-1-state.json` e `g8-2-state.json` são contratos de estado estáticos e versionados, consumidos por `scripts/cloud/Test-G81Static.py` e `scripts/cloud/Test-G82Static.py`.
- Estes ficheiros não são produzidos por execução cloud antes dos validadores; um clone limpo deve recebê-los com o repositório.
- Não devem conter segredos, ADC, tokens, Billing Account IDs completos, dados pessoais ou evidência sintética de execução cloud.
