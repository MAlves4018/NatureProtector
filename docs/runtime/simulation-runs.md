---
title: Simulation runs
status: CURRENT
verified_at: 2026-07-22
source_of_truth:
  - src/NatureProtector.Backoffice.Api/Controllers/RuntimeControlController.cs
  - src/NatureProtector.Backoffice.Api/Services/RuntimeOperationService.cs
  - src/NatureProtector.Simulator.Host
  - scripts/validation/Invoke-LocalFunctionalValidation.ps1
---

# Simulation runs

As simulações locais devem ser executadas pelo **Run Orchestrator**, através da UI `/simulation`, da API de control plane ou do harness funcional. O `NatureProtector.Simulator.Host` é um produtor bounded lançado pela operação e não deve permanecer como processo residente depois da run.

## Percurso canónico

1. Preparar e arrancar a baseline com `scripts/np.ps1`.
2. Abrir `http://localhost:5173/login` e autenticar uma identidade com `run:start`.
3. Navegar para `/simulation`.
4. Escolher área, cenário, número de sensores, ciclos, intervalo, seed e perfis de degradação.
5. Iniciar a run e acompanhar a operação até ao estado terminal.
6. Confirmar o detalhe persistido em `/runs`, as consultas em `/queries` e, quando aplicável, a comparação em `/scenario-compare`.

O endpoint de criação é:

```text
POST /api/control/runtime/runs
```

Pedido nominal curto:

```json
{
  "areaCode": "proenca-a-nova",
  "scenarioCode": "scenario_b",
  "sensorCount": 6,
  "numberOfCycles": 5,
  "intervalSeconds": 1,
  "seed": 12345,
  "degradationProfiles": ["none"]
}
```

A resposta e o acompanhamento relacionam três identificadores diferentes:

- `requestId`: correlação idempotente do pedido;
- `operationId`: operação assíncrona do control plane;
- `runId`/`SimulationRunId`: identidade funcional persistida da simulação.

## Estados e conclusão

Uma run só pode ser considerada concluída quando:

- a operação chegou ao estado terminal esperado;
- o processo `Simulator.Host` terminou;
- o audit persistido convergiu;
- não existem mensagens indefinidamente em `Processing`;
- os contadores observados satisfazem o contrato do perfil executado.

O fim do processo produtor não prova, por si só, a convergência do pipeline. RabbitMQ, Prevention Host e projeções podem terminar o processamento alguns instantes depois. O harness deve esperar pela condição **settled**, não por um atraso fixo arbitrário.

## Cenários B/C de smoke funcional

A campanha funcional existente usa:

- `scenario_b` com `none`, para o caminho nominal;
- `scenario_c` com `missing-readings`, para provar omissão controlada e comparação degradada.

Com `S` sensores e `C` ciclos:

```text
expectedObservations = S × C
```

No caminho nominal, o contrato mínimo é:

```text
acceptedObservations = expectedObservations
riskAssessments = acceptedObservations
missing = 0
rejected = 0
quarantined = 0
```

Com `missing-readings`, o contrato mínimo é:

```text
0 < acceptedObservations < expectedObservations
missing = expectedObservations - acceptedObservations
riskAssessments = acceptedObservations
```

Valores concretos de campanhas passadas, como 30/30 no cenário B e 24/30 no cenário C, são evidência histórica ligada àquela configuração e seed. Não constituem constantes universais nem substituem uma execução atual.

## Perfis e invariantes

Os contratos dos doze perfis suportados estão definidos em:

- `docs/reference/scenario-acceptance-invariants.md`;
- `config/acceptance/scenario-invariants.json`.

Cada perfil exige asserções próprias. Uma run que apenas chega a `Completed` não prova que `bias`, `drift`, `duplicate`, `out-of-order`, `retry-transient` ou outro perfil produziu o efeito correto.

## Harness atual

Validação funcional local:

```powershell
.\scripts\validation\Invoke-LocalFunctionalValidation.ps1 -Full -Evidence -Ui
```

O harness existente prova sobretudo o par B/C. A matriz completa de perfis fica como entrada formal para a Fase 2, onde será consolidada num runner de aceitação único.

## Consultas de acompanhamento

```text
GET /api/control/runtime/operations/{operationId}
GET /api/control/runtime/operations/by-request/{requestId}
GET /api/control/runtime/runs/{runId}
GET /api/control/runtime/runs/{runId}/operation
GET /api/control/runtime/runs/{runId}/audit
GET /api/control/runtime/runs/{runId}/timings
```

Não confundir:

- estado do processo produtor;
- estado da operação do control plane;
- estado persistido da run;
- convergência funcional do pipeline.

O veredicto final deve resultar da combinação destas quatro superfícies.
