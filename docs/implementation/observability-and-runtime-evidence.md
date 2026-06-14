# Observability and runtime evidence

Last updated: 2026-06-14

This document describes the current internal observability slice. It is runtime evidence for a technical prototype, not scientific validation of wildfire risk, official alerting, or calibrated prediction.

## Health model

The Backoffice keeps the simple technical `/health` endpoint for basic readiness.

Detailed operational health is exposed through:

```text
GET /api/control/runtime/observability/health
```

The endpoint is authenticated with the existing `Sim`, `Pipeline` or `Admin` roles. It does not add roles or claims.

Component status is explicit:

```text
Healthy
Degraded
Unhealthy
Unknown
NotInstrumented
NotApplicable
```

Absence of errors is not treated as `Healthy`. Missing or inaccessible signals are represented as `Unknown`, `NotInstrumented` or `NotApplicable`.

Current components:

- `Backoffice.Api`: positive signal from the authenticated request reaching the controller.
- `PostgreSQL`: EF Core connectivity probe.
- `RabbitMQ`: RabbitMQ Management HTTP API and relevant queue state.
- `Prevention.Host`: proxy signal from consumers on `np.ingestion.readings`.
- `Simulator.Host`: latest simulation run lifecycle; a completed run is `NotApplicable`, not unhealthy.
- `InfluxDB`: HTTP health probe; unauthorized or unreachable probes are `Unknown`.
- `Grafana`: `/api/health` with database status where available.

## RabbitMQ metrics

RabbitMQ queue metrics are exposed through:

```text
GET /api/control/runtime/observability/rabbitmq
```

Per queue:

```text
QueueName
MessagesReady
MessagesUnacknowledged
MessagesTotal
Consumers
ObservedAt
Source
CollectionStatus
Limitation
```

The endpoint uses the RabbitMQ Management HTTP API. It does not expose credentials.

Unavailable metrics are nullable and marked with `CollectionStatus`. They are not converted to zero. A zero value means RabbitMQ reported zero.

Backlog is reported explicitly as ready, unacknowledged and total message counts. The UI v2 does not collapse these values into an ambiguous single backlog number.

## Timestamps and correlation

The published RabbitMQ contract remains:

```text
EventEnvelope<TPayload>
SensorReadingProduced
```

It contains `EventTime`, optional `IngestTime`, `EventId` and `CorrelationId`. It does not contain a persisted `PublishedAt`.

This means publish-to-end latency is still gated. The system may show persisted run/inbox/processing/risk timestamps, but it must not claim full end-to-end latency until comparable publish/receive/process timestamps exist.

## Run-scoped audit and timings

Run audit:

```text
GET /api/control/runtime/runs/{runId}/audit
```

Run timings:

```text
GET /api/control/runtime/runs/{runId}/timings
```

Both continue to read persisted runtime records only. They do not recalculate risk.

The responses now include optional `dataScope` metadata:

```text
RequestedRunId
ResolvedRunId
DataRunId
ObservedAt
Source
Scope
Limitations
```

Timings also include an ordered `timeline` of measured persisted points when available:

```text
requested
started
first_received
first_processing_started
first_risk_assessment
first_alert
last_processing_finished
completed
```

Only measured persisted points are included. Stopwatch log durations remain logs unless/until a structured run timing persistence model is added.

## Quality and classifiers

Current audit quality data remains partial:

- quality flag summary is derived from persisted accepted-reading operational states and missing-event arithmetic;
- eligibility summary is derived from persisted risk assessment explanation summaries and accepted/risk count differences;
- detailed classifier payloads are not persisted as aggregate runtime projections.

No scoring, eligibility semantics, `Blocked`, `PartialButUsable`, `CompleteEligible`, quality flag meaning or classifier meaning was changed.

Detailed classifier/quality persistence remains owner-review work because it needs additive schema, retention and payload-size decisions.

## Evidence HTTP

Evidence is exposed through an allowlisted HTTP catalog:

```text
GET /api/control/runtime/observability/evidence
GET /api/control/runtime/observability/evidence/{evidenceId}
```

Rules:

- source is limited to `docs/evidence`;
- public identifiers are generated evidence IDs, not filesystem paths;
- extensions are allowlisted to `.md`, `.txt`, `.json` and `.csv`;
- the catalog returns the 250 most recent allowlisted files and reports `evidence_catalog_truncated` when more exist;
- content is capped at 1 MiB;
- canonical path validation prevents traversal outside `docs/evidence`;
- responses use `no-store`;
- the Brain folder, `.env`, `.git`, arbitrary paths and binary files are not exposed.

## UI Pipeline

UI v2 Pipeline consumes the new observability contracts proportionally:

- service health appears as technical fields;
- RabbitMQ ready/unacknowledged/consumer metrics appear only when measured;
- unavailable queue values remain unavailable, not zero;
- publisher timestamps remain `NotInstrumented`;
- current/global projections are still labelled as current projections when not guaranteed run-scoped;
- run audit and timings continue to be preferred for selected run details.

## Grafana and InfluxDB

This pass did not create Grafana dashboards. Grafana health is probed through the real health endpoint and can be shown in operational health.

InfluxDB health is probed through HTTP. If the local endpoint requires authorization and no configured token is available to Backoffice, the component is `Unknown`, not `Healthy`.

## Validation commands

Executed during this pass:

```powershell
dotnet test tests\NatureProtector.Backoffice.Api.Tests\NatureProtector.Backoffice.Api.Tests.csproj --no-restore
dotnet test NatureProtector.sln --no-build --no-restore -m:1
npm run typecheck
npm test -- src/app/ui-v2/technicalSurfaces.test.ts
npm test -- src/app/ui-v2 src/app/services/api.test.ts
npm run test:coverage -- src/app/ui-v2 src/app/services/api.test.ts
```

Runtime smoke evidence was captured under:

```text
NatureProtector.brain/control/OBSERVABILITY-AND-RUNTIME-EVIDENCE-001/
```

Observed runtime smoke on 2026-06-14:

- `Backoffice.Api=Healthy`
- `PostgreSQL=Healthy`
- `RabbitMQ=Degraded`
- `Prevention.Host=Healthy`
- `Simulator.Host=NotApplicable`
- `InfluxDB=Unknown`
- `Grafana=Healthy`
- `np.ingestion.readings`: ready `0`, unacknowledged `0`, total `0`, consumers `1`
- `np.observability.raw`: ready `52`, unacknowledged `0`, total `52`, consumers `0`
- evidence catalog returned the 250 most recent allowlisted items and marked `evidence_catalog_truncated`
- evidence catalog returned HTTP content for an allowlisted item and rejected traversal with `400`

## Remaining limitations

- No RabbitMQ `PublishedAt` without owner-approved contract/instrumentation work.
- No detailed classifier payload persistence or aggregate quality projection persistence yet.
- No Grafana dashboard was created in this pass.
- No full e2e event latency is claimed.
- Historical runs before future quality/classifier persistence will still lack detailed classifier evidence.
- `np.observability.raw` may show backlog with zero consumers by design unless a consumer is expected and provisioned.
