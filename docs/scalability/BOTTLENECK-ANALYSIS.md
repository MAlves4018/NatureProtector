# Bottleneck Analysis

First confirmed bottleneck: InfluxDB write path / telemetry persistence in the critical processing path.

Evidence:

- With Influx enabled, the H2 mean throughput was 0.976667 events/s.
- With Influx disabled through the supported `InfluxDb__Enabled=false` mode, the H2 mean throughput was 6.247667 events/s.
- P95 latency and peak backlog also dropped in the Influx-disabled variant.
- H1/prefetch 1, 2, 4 and 8 did not materially improve throughput.

Limitation: Influx-disabled is diagnostic A/B evidence, not a permanent recommendation to remove telemetry.
