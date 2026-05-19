# Runtime notes

Validation window: 2026-05-18 23:15 UTC to 2026-05-18 23:18 UTC.

## Scenario B

- Spec: `docs/evidence/progress-2026-05-22/scenario-b-5cycles-6sensors-none.json`
- Evidence output: `docs/evidence/runs/20260519-011514-scenario_b-scenario-b-5cycles-6sensors-smoke/`
- SimulationRunId: `d8203d4b-1839-4908-87ef-05633c1f1ae5`
- Status: `Completed`
- Expected events: 30
- Inbox events: 30
- Risk assessments: 30
- Missing events: 0
- Rejected: 0
- Quarantined: 0
- Effective degradation profile: `none`

## Scenario C

- Spec: `docs/evidence/progress-2026-05-22/scenario-c-5cycles-6sensors-missing-readings.json`
- Evidence output: `docs/evidence/runs/20260519-011713-scenario_c-scenario-c-5cycles-6sensors-missing-readings/`
- SimulationRunId: `36caca67-352c-41f1-80e3-8fe951a1582c`
- Status: `Completed`
- Expected events: 30
- Inbox events: 24
- Risk assessments: 24
- Missing events: 6
- Rejected: 0
- Quarantined: 0
- Effective degradation profile: `missing-readings`

## Notes

- The direct script invocation was blocked by the local PowerShell execution policy, so runs were executed with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ...`.
- Docker access required execution outside the sandbox to query PostgreSQL.
- Historical rejected/quarantined rows were not used as evidence for these fresh runs.
- `EventEnvelope<SensorReadingProducedPayload>` was not changed.
