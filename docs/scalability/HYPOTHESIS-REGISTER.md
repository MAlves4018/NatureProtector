# Hypothesis Register

| ID | Hypothesis | Current result |
|---|---|---|
| H1 | One replica processes around one event/s in local runtime | Supported; fixed protocol found stable 0.5 events/s and first unstable 1.0 events/s for one replica. |
| H2 | Queue growth was missed without continuous sampling | Supported; current campaign preserves BACKLOG_TIMELINE.csv. |
| H3 | More replicas improve throughput initially | Supported in dynamic autoscaling rows, best speedup 4.58 vs S1. |
| H4 | PostgreSQL becomes bottleneck with several replicas | Evaluated indirectly; not first confirmed bottleneck in current A/B. |
| H5 | InfluxDB limits scale | Confirmed in diagnostic A/B: disabling Influx increased H2 mean throughput from 0.976667 to 6.247667 events/s and reduced backlog/latency. |
| H10 | Queue depth alone is insufficient | Partially addressed by combined RabbitMQ/PostgreSQL work signal. |
