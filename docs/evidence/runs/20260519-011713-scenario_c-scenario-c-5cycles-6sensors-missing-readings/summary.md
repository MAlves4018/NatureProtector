# Scenario Run Summary

- branch: master
- commit: 8dac9da9408d99b14c26ea312da4d3c536eeafcb
- runLabel: scenario-c-5cycles-6sensors-missing-readings
- areaCode: proenca-a-nova
- scenarioCode: scenario_c
- simulationRunId: 36caca67-352c-41f1-80e3-8fe951a1582c
- finalStatus: Completed
- hostExitCode: 
- outputDir: C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\runs\20260519-011713-scenario_c-scenario-c-5cycles-6sensors-missing-readings
- collectEvidence: True
- evidenceResult: requested_completed

## Requested Parameters

- sensorCount: 6
- numberOfCycles: 5
- intervalSeconds: 5
- seed: 12345
- degradationProfile: missing-readings
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
