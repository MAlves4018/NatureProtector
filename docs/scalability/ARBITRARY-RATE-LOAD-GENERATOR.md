# Arbitrary-Rate Load Generator

`NatureProtector.Simulator.Host` can run in temporal load mode with `TemporalLoad:Enabled=true`.

The mode loads a workload catalog, resolves the normal control-plane simulation context, creates a persisted `SimulationRun`, and publishes real sensor-reading envelopes through the existing RabbitMQ publisher. It does not introduce a new event contract.

Recorded raw data per run:

- `identity.json`
- `configuration.json`
- `workload.json`
- `events.csv`
- `summary.json`
- `receipt.json`

The scheduler supports constant, burst and ramp segments. Spike, step and rise-hold-fall workloads are represented as ordered segment combinations in `config/autoscaling/temporal-workloads.json`.

Status: implemented and unit-tested. Runtime evidence must cite the generated raw files, not this documentation.
