# Phase 6 — reliability, retry, quarantine and recovery evidence

Phase 6 separates four different claims:

1. **Static reliability contract** — inbox states, retry policy, failure classification, idempotency, quarantine, lease recovery, telemetry and tests.
2. **Controlled P3 execution** — bounded negative-pipeline messages in `Development` or `Evidence` only.
3. **Run-specific PostgreSQL audit** — query pack 11 verifies the exact outcomes for the exact P3 `run_label`.
4. **Infrastructure outage recovery** — RabbitMQ/PostgreSQL/Influx or process outage drills, which are not proved by P3 and remain separate.

## Static collection

```powershell
& .\scripts\evidence\collect-reliability-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe"
```

This does not publish messages, start services, change data, execute Docker, alter cloud resources or run deployment actions. It makes short availability probes only.

## Controlled P3 execution

Start the local runtime in `Development` or `Evidence`, ensure there are no active runs, and use a temporary Sim/Admin token:

```powershell
$env:NP_RELIABILITY_AUTH_TOKEN = "<temporary Sim or Admin token>"
$runLabel = "controlled-validation-p3-negative-pipeline-$(Get-Date -Format 'yyyyMMdd-HHmmss')-report"

& .\scripts\evidence\collect-reliability-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe" `
  -ExecuteP3 `
  -AcknowledgeNonProduction `
  -P3RunLabel $runLabel `
  -RequireP3
```

The runner first calls `GET /api/dev/controlled-validation/p3` and refuses to execute unless the API reports `Development` or `Evidence`. It accepts no arbitrary event payloads, sensor IDs, areas, routing keys or fault lists.

A successful endpoint response is still **audit required**. The endpoint does not safely execute query pack 11 itself.

## PostgreSQL audit for the exact run

Use the same `$runLabel`:

```powershell
$auditRoot = ".\artifacts\report-evidence\baseline-YYYYMMDDTHHMMSSZ\06-reliability\postgres-audit-$((Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'))"

& .\tools\data-audit\run-postgres-audit.ps1 `
  -ConnectionString $env:NP_POSTGRES_CONNECTION_STRING `
  -OutputRoot $auditRoot `
  -RunId "p3" `
  -ControlledValidationRunLabel $runLabel
```

Then ingest and require the P3 audit:

```powershell
& .\scripts\evidence\collect-reliability-evidence.ps1 `
  -BaselineId "baseline-YYYYMMDDTHHMMSSZ" `
  -PythonExecutable "C:\Users\Miguel\AppData\Local\Programs\Python\Python313\python.exe" `
  -AuditDirectory "$auditRoot\p3\postgres" `
  -RequireAudit
```

The audit passes only when:

- query pack 11 returns 12 expected case rows;
- all 10 executable cases match their expected path;
- both fixture-dependent cases remain `blocked_needs_fixture`;
- both retry paths match;
- there are no unexpected accepted readings or risk assessments for negative cases.

## Required report metrics

Present at least:

- executable, matched and blocked cases;
- rejected and quarantined events by reason code;
- retry-scheduled, succeeded and quarantined attempts;
- retry path per fault case;
- unexpected positive projections for negative cases;
- configured retry delays versus observed attempt timestamps;
- exact baseline, run label, environment, command, raw output location and SHA-256.

## Claim boundaries

Do not claim:

- production resilience or availability from P3;
- RabbitMQ/PostgreSQL/Influx outage recovery from injected processing exceptions;
- recovery time from configured delay values;
- zero event loss without complete run-specific reconciliation;
- support for `sensor_inactive`, `sensor_area_mismatch` or out-of-order semantics while they remain blocked;
- a complete operational recovery workflow while quarantine replay/manual retry is not exposed as an audited command.
