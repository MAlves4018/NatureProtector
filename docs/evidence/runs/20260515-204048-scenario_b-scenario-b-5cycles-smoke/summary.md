# Scenario Run Summary

- branch: docs/v1-implementation-plan
- commit: f29f0865d6082a4a7dc5cc60fabb45004d9a51d0
- runLabel: scenario-b-5cycles-smoke
- areaCode: proenca-a-nova
- scenarioCode: scenario_b
- simulationRunId: 9b937806-b2a2-46fb-9a55-a99f6d29235b
- finalStatus: TimedOut
- hostExitCode: 
- outputDir: C:\Users\Miguel\UNI\6sem\PS\IMP\A\NatureProtector\docs\evidence\runs\20260515-204048-scenario_b-scenario-b-5cycles-smoke
- collectEvidence: False
- evidenceResult: not_requested

## Requested Parameters

- sensorCount: 12
- numberOfCycles: 5
- intervalSeconds: 5
- seed: 12345
- degradationProfile: none
- waitForCompletion: True
- timeoutSeconds: 900
- allowParallelRun: False

## Resolved/Observed

- numberOfCycles: 20 [requested_not_confirmed_or_not_applied]
- intervalSeconds: 30 [requested_not_confirmed_or_not_applied]
- seed: 12345 [observed_match]
- sensorCount: 6 [requested_not_confirmed_or_not_applied]

## Limitations

- O1.1 does not yet guarantee Host support for Simulator:RunOverrides:*.
- sensorCount is tracked as requested and may be pending host support until O1.2.
- orchestratorCorrelationId lookup may fallback to time-window matching if MetadataJson does not include it yet.

## Next Steps

- Move to O1.2 to implement and persist Host support for Simulator:RunOverrides:* in MetadataJson.
- Add deterministic sensor subset selection in Host for true sensorCount application.
- Correlate runs directly by orchestratorCorrelationId once Host metadata support is available.
