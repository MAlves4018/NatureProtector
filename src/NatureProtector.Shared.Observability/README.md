# NatureProtector.Shared.Observability

This project contains runtime observability wiring for NatureProtector hosts.

## Scope

- OpenTelemetry host registration.
- Console and OTLP exporter registration.
- ASP.NET Core, HttpClient, runtime and process instrumentation.
- Shared `ActivitySource`, `Meter` and metric names used by runtime hosts.
- Logging activity tracking options.

## Boundary

`NatureProtector.Shared` remains the contracts and messaging boundary. It must not depend on `OpenTelemetry*` packages.

The process instrumentation package is still beta in the current package line. The project keeps it isolated here so pure contracts and consumers that only need message contracts do not inherit exporter or instrumentation dependencies.

The smoke tests in `NatureProtector.Shared.Tests` validate startup compatibility with OTLP configuration. They do not prove delivery to a real collector.
