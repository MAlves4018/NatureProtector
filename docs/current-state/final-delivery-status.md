# NatureProtector final delivery status after phases 7-10

## Implemented

- hosted reconciliation of non-terminal runtime operations;
- explicit long-run proof matrix and termination manifests;
- E1-E6 evidence campaign with run/operation identity and hashes;
- capacity-derived KEDA experiment generation with RabbitMQ and retry/settlement signals;
- operational and domain time-series catalog for InfluxDB 3;
- five provisioned Grafana dashboards;
- near-real-time IPMA polling with deduplication and provenance;
- conservative removal of superseded frontend surfaces and the setup-only dashboard.

## Proven in this repository workspace

- all enforce-mode quality gates available in the sandbox;
- frontend strict typecheck, lint, format and 76 tests;
- Python tests for long-run manifests, E1-E6 evidence, capacity analysis, IPMA ingestion, operational line protocol, dashboards and final-delivery contract;
- synthetic artifact pipelines and deterministic hash verification.

## Runtime gates still requiring an equipped environment

The following names are reserved for live proof and must not be claimed from static or synthetic validation:

- `LONG_RUN_AND_ORCHESTRATION_RUNTIME_PASS`;
- `SYSTEM_RESET_AND_RECOVERY_PASS`;
- `MULTI_REPLICA_TEMPORAL_CORRECTNESS_PASS`;
- `REPORT_EVIDENCE_PORTFOLIO_READY`;
- `AUTOSCALING_REALTIME_OBSERVABILITY_PROVED`;
- `MERGE_CANDIDATE_READY`.

They require the .NET 9 SDK, PowerShell, Docker/PostgreSQL/RabbitMQ/InfluxDB/Grafana and, for Cloud Run or GKE claims, explicit staging authorization. Missing tools are a test-environment limitation, not a passing result.

## Delivery decision

The source and harnesses for phases 7-10 are implemented and statically validated. Merge remains blocked until the generated Codex mission executes the live matrices, records the outputs and updates this document with commit, environment and artifact identities.

## External weather data scope

The IPMA adapter is currently **observability-only**: it writes provenance-rich near-real-time observations to InfluxDB for Grafana and evidence. It is not yet an input to RabbitMQ, Prevention, or the risk calculation, and no report claim may describe it as a domain decision input.
