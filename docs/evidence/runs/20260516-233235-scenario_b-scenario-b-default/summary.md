# Scenario Run Summary

- branch: master
- commit: 8f66b2474654bcc3c3267a345cd1ce59732a3d33
- runLabel: scenario-b-default
- areaCode: proenca-a-nova
- scenarioCode: scenario_b
- simulationRunId: 
- finalStatus: HostFailedBeforeRun
- hostExitCode: 
- outputDir: C:\Users\Miguel\UNI\6sem\PS\IMP\D\NatureProtector\docs\evidence\runs\20260516-233235-scenario_b-scenario-b-default
- collectEvidence: True
- evidenceResult: requested_completed

## Requested Parameters

- sensorCount: 12
- numberOfCycles: 20
- intervalSeconds: 30
- seed: 12345
- degradationProfile: none
- waitForCompletion: True
- timeoutSeconds: 900
- allowParallelRun: True

## Resolved/Observed

- numberOfCycles:  [requested_not_confirmed]
- intervalSeconds:  [requested_not_confirmed]
- seed:  [requested_not_confirmed]
- sensorCount:  [requested_not_confirmed_pending_host_support]

## Limitations

- Run status uses PostgreSQL (control.simulation_runs) as source of truth.
- Overrides are considered confirmed when observed in SimulationRun fields and/or SimulationRun.MetadataJson.
- HostFailedBeforeRun reason: BackgroundService failed

## Next Steps

- If status and metadata are correct, proceed with operational hardening (timeouts/retries/reporting).
- Keep run-spec and metadata fields stable for future Backoffice/API orchestration reuse.
