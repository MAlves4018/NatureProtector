# Simulation runs

As simulacoes devem ser executadas pelo Run Orchestrator, pela API/control plane ou pelo harness funcional local. O `Simulator.Host` nao deve ser arrancado como processo persistente.

## Cenarios de freeze candidate

O freeze candidate local usa dois cenarios funcionais:

- `scenario_b`: caminho nominal limpo.
- `scenario_c`: caminho degradado com `missing-readings`.

Resultado funcional aceite no fecho de RFX-001:

| Cenario | Accepted | Risk assessments | Missing | Rejected | Quarantined |
| --- | ---: | ---: | ---: | ---: | ---: |
| `scenario_b` | 30 | 30 | 0 | 0 | 0 |
| `scenario_c` | 24 | 24 | 6 | 0 | 0 |

## Convergencia assíncrona

O audit de uma run pode convergir depois de o processo de simulacao terminar. O harness `scripts\validation\Invoke-LocalFunctionalValidation.ps1` deve aguardar essa convergencia antes de declarar PASS/FAIL para B/C.

Uma leitura parcial do audit nao deve ser promovida automaticamente a regressao funcional sem confirmar o estado final persistido.

