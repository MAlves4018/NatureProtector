# Scenario Run Summary

- branch: docs/v1-implementation-plan
- commit: f29f0865d6082a4a7dc5cc60fabb45004d9a51d0
- runLabel: scenario-b-5cycles-6sensors-smoke
- areaCode: proenca-a-nova
- scenarioCode: scenario_b
- simulationRunId: 2f8bf6cc-4ffe-4f64-ada7-fe8e3380ae67
- finalStatus: TimedOut
- hostExitCode: 
- outputDir: C:\Users\Miguel\UNI\6sem\PS\IMP\A\NatureProtector\docs\evidence\runs\20260515-213331-scenario_b-scenario-b-5cycles-6sensors-smoke
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
- sensorCount: 6 [observed_match_unconfirmed_host_override_support]

## Limitations

- O1.1 does not yet guarantee Host support for Simulator:RunOverrides:*.
- sensorCount is tracked as requested and may be pending host support until O1.2.
- orchestratorCorrelationId lookup may fallback to time-window matching if MetadataJson does not include it yet.

## Next Steps

- Move to O1.2 to implement and persist Host support for Simulator:RunOverrides:* in MetadataJson.
- Add deterministic sensor subset selection in Host for true sensorCount application.
- Correlate runs directly by orchestratorCorrelationId once Host metadata support is available.
