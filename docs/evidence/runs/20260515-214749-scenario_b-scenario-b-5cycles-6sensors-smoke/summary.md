# Scenario Run Summary

- branch: docs/v1-implementation-plan
- commit: f29f0865d6082a4a7dc5cc60fabb45004d9a51d0
- runLabel: scenario-b-5cycles-6sensors-smoke
- areaCode: proenca-a-nova
- scenarioCode: scenario_b
- simulationRunId: c8a664e0-8145-40f9-bb00-aaf5a51ebec0
- finalStatus: TimedOut
- hostExitCode: 
- outputDir: C:\Users\Miguel\UNI\6sem\PS\IMP\A\NatureProtector\docs\evidence\runs\20260515-214749-scenario_b-scenario-b-5cycles-6sensors-smoke
- collectEvidence: False
- evidenceResult: not_requested

## Requested Parameters

- sensorCount: 6
- numberOfCycles: 5
- intervalSeconds: 5
- seed: 12345
- degradationProfile: none
- waitForCompletion: True
- timeoutSeconds: 180
- allowParallelRun: False

## Resolved/Observed

- numberOfCycles: 5 [observed_match]
- intervalSeconds: 5 [observed_match]
- seed: 12345 [observed_match]
- sensorCount: 6 [observed_match]

## Limitations

- Run status uses PostgreSQL (control.simulation_runs) as source of truth.
- Overrides are considered confirmed when observed in SimulationRun fields and/or SimulationRun.MetadataJson.

## Next Steps

- If status and metadata are correct, proceed with operational hardening (timeouts/retries/reporting).
- Keep run-spec and metadata fields stable for future Backoffice/API orchestration reuse.
