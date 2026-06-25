# Runtime run orchestration after G1

## Decision boundary

`PostgresControlPlaneService` validates requests, reads persisted state and maps API responses. It no longer creates operating-system processes, resolves the solution directory, builds command lines or terminates process trees.

The runtime boundary is:

```text
ControlRuntimeController
  -> IControlPlaneService
    -> PostgresControlPlaneService
      -> IRuntimeRunOrchestrator
      -> IRuntimeEvidenceSink
```

## Contract

`IRuntimeRunOrchestrator` receives a typed `RuntimeLaunchRequest` and exposes:

- `StartAsync` with an idempotency key;
- `GetAsync` for provider-neutral execution state;
- `StopAsync` with an explicit stop reason.

The request carries simulation parameters and, for P3, a fixed controlled-validation payload. It does not carry a shell command, GCP resource name, PID or repository path.

## Adapters

### Disabled

Default outside explicit local profiles. Validation remains available but no process or cloud job is started.

### LocalProcess

Allowed only in `Development` or `Evidence`. It preserves the current local workflow by launching either the Simulator project or a published Simulator assembly. All process APIs are isolated in this adapter.

### Future cloud adapter

Not implemented in G1. A future adapter may translate the same contract into a Cloud Run Job execution or Kubernetes Job without altering the control-plane service.

## Evidence boundary

`IRuntimeEvidenceSink` separates evidence persistence from launch. Filesystem evidence is restricted to `Development`/`Evidence`; the default container profile uses the null sink and therefore does not require a writable application filesystem.

## Idempotency scope

The local adapter deduplicates by idempotency key within one API process. This is not distributed coordination. Before the API can safely scale out while starting cloud jobs, the idempotency key and provider execution reference must be persisted transactionally.

## Security constraints

- `LocalProcess` is rejected outside Development/Evidence.
- No arbitrary command, argument list or environment-variable name is accepted from the HTTP request.
- P3 continues to use fixed cases and typed fields.
- Production containers set orchestration and evidence to `Disabled`.
