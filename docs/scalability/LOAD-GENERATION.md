# Load Generation

The maintained live path still supports the runtime launch API with controlled sensor count, cycle count, interval seconds, seed and degradation profiles.

For S16-R1, `NatureProtector.Simulator.Host` also supports `TemporalLoad:Enabled=true`. This mode reuses the existing `ReadingGenerationService` and `IReadingPublisher` path to publish real `EventEnvelope<SensorReadingProducedPayload>` messages to RabbitMQ with publisher confirms. It decouples offered rate from active sensor count by scheduling one reading at a time across the resolved sensor set.

The temporal scheduler uses a monotonic due-time model:

```text
next_due = active_start + event_index / requested_rate
```

The raw event CSV records `SimulationRunId`, `EventId`, `CycleIndex`, `GridCellId`, due offset, actual scheduler elapsed time, schedule delay and confirmation timestamp. Rate precision is calculated over the configured active workload window, while the confirmation window is preserved separately in `summary.json`.

Status: implemented and covered by focused unit tests. Live scalability claims still require running the temporal harness modes and compiling the resulting raw CSVs.
