# G1 — Container Readiness & Local Orchestration Decoupling

Date: 2026-06-19

## Objective

Prepare deployment units and remove the Backoffice API's direct dependency on child processes and the repository tree, without provisioning cloud infrastructure or changing domain semantics.

## Implemented

### Runtime orchestration

- introduced provider-neutral contracts under `NatureProtector.Backoffice.Api/RuntimeOrchestration`;
- moved all `Process` use to `LocalProcessRuntimeRunOrchestrator`;
- restricted local process and filesystem evidence adapters to Development/Evidence;
- made orchestration and filesystem evidence disabled by default;
- removed the development JWT signing key from the base/Production configuration and added fail-fast JWT option validation;
- kept HTTP request validation, run lookup and response contracts in `PostgresControlPlaneService`;
- made runtime evidence root explicit rather than discovered by walking to `NatureProtector.sln`.

### Container units

Multi-stage, non-root Dockerfiles were added for:

- Backoffice API;
- Prevention Host;
- Simulator Host;
- PostgreSQL Bootstrap;
- React/Vite frontend.

The API and Prevention expose HTTP liveness/readiness. Simulator and bootstrap remain finite one-shot processes and deliberately have no service readiness endpoint.

### Messaging diagnostic queue

`np.observability.raw` is now optional and disabled by default. When enabled it is declared with:

- message TTL;
- maximum message count;
- maximum bytes;
- `drop-head` overflow.

`np.ingestion.readings` and its functional semantics are unchanged.

> **Current-state correction — 2026-07-13:** the executable baseline `NatureProtector-master (16)` does not implement this diagnostic-queue claim. `RabbitMqOptions` has no `ObservabilityRawEnabled` property, all declarers bind `np.observability.raw`, and local/Compose provide no TTL or length arguments. The cloud policy applies `reject-publish` to every `np.*` queue. Treat the paragraph above as a historical target, not current runtime proof, until ADR RMQ-01 is implemented and revalidated.

### Local compose

`docker-compose.g1.yml` overlays the existing infrastructure compose. It keeps the Simulator and bootstrap behind explicit profiles and sets application filesystems read-only.

## Not implemented

- no Google Cloud adapter;
- no Artifact Registry;
- no Terraform;
- no cloud IAM or Secret Manager integration;
- no migration job;
- no image signing or remote provenance;
- no production rollout.

## Evidence status

Static validation was completed in the analysis environment. Frontend dependency installation, typecheck, lint, formatting, 47 tests, production build and audit policy were executed successfully. Backend build/test and container runtime evidence remain `BLOCKED_BY_EXECUTION_ENVIRONMENT` because .NET, Docker and PowerShell were unavailable. The owner-side evidence script is the authority for promotion of those areas to `PROVED_IN_OWNER_ENVIRONMENT`.

## Deferred risks carried forward

- The repository's pinned .NET SDK/runtime pairing is preserved for reproducibility, but it is behind the current supported patch and must be upgraded and revalidated before a cloud release.
- The existing API startup administrative bootstrap remains for local compatibility. It is not the G0 migration/bootstrap target and must be separated before staging. The G1 container path no longer supplies a fixed default admin password.
- The G1 owner script creates a process-local random JWT key when none is supplied, and validates Compose with `config --quiet` so the resolved secret is not persisted in evidence.
- The local orchestration adapter retains repository discovery only inside the Development/Evidence boundary. It is neither distributed nor suitable for cloud selection.
- G1 proves a single Prevention container topology by design; multi-instance runtime proof remains the dedicated G0/G1 owner-side evidence activity.
