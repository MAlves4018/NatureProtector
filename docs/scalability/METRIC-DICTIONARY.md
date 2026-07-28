# Metric Dictionary

All scalability values are local observed evidence. Do not compare metrics unless the source window, topology and denominator match.

| Metric | Unit | Formula | Source | Window | Denominator | Filters | Limitation |
|---|---:|---|---|---|---|---|---|
| requested offered rate | events/s | configured target event rate | harness configuration / raw result row | active load | configured seconds | none | may differ from actual publication rate because the generator uses integer sensors and intervals |
| actual publish rate | events/s | published events / publish window seconds | `FIXED_REPLICA_RAW_RESULTS.csv`, latency exports | active load | publish window | successful publication attempts | not a completion metric |
| broker-confirmed publish rate | events/s | broker-confirmed messages / publish window seconds | RabbitMQ samples when available | active load | publish window | confirmed messages | absent when the source run does not expose confirm counts |
| accepted rate | events/s | accepted events / active-load seconds | API/run accounting | active load | active-load seconds | accepted events only | not equivalent to processing completion |
| ingress rate | events/s | received or inbox rows / ingress window seconds | PostgreSQL inbox and RabbitMQ samples | active load | ingress window | correlated run rows | can lag publisher under backlog |
| completion rate | events/s | processed events / processing or drain window seconds | `processed`, `completed_throughput`, `processed_rate` | processing/drain | time-to-drain or explicit processing window | correction-passing rows | includes drain when the source row uses time-to-drain |
| peak throughput | events/s | max completion rate across comparable repetitions or time slices | phase aggregate CSV | same workload window | same as completion rate | correction-passing rows | not capacity unless sustained and stable |
| sustained throughput | events/s | mean completion rate across repetitions for same point | phase aggregate CSV | identical point repetitions | repetition count | correction-passing rows | depends on selected window |
| stable capacity | events/s | highest requested offered rate whose repetitions meet stability gates | `capacity-by-replica.csv` | fixed-topology campaign | offered-rate grid | majority stable, correction pass | local observed limit only |
| processing throughput | events/s | processed events / processing window | latency/accounting exports | processing window | processing seconds | processed events | not comparable to active-load rate unless windows match |
| end-to-end throughput | events/s | completed run events / campaign elapsed seconds | run accounting | campaign total | total elapsed seconds | completed events | includes setup/drain if not separated |
| backlog | work items | RabbitMQ ready + unacknowledged + persisted pending work where available | `BACKLOG_TIMELINE.csv` | sampled timeline | sample timestamp | run-correlated samples | one-second sampling can miss transient peaks |
| RabbitMQ ready | messages | queue `messages_ready` | RabbitMQ management sample JSON | sampled timeline | sample | queue `np.ingestion.readings` | broker-specific |
| RabbitMQ unacknowledged | messages | queue `messages_unacknowledged` | RabbitMQ management sample JSON | sampled timeline | sample | queue `np.ingestion.readings` | broker-specific |
| inbox pending | rows | count inbox rows with pending status | PostgreSQL work samples | sampled timeline | sample | run-independent table state after reset | sample-level, not event-level latency |
| inbox processing | rows | count inbox rows with processing status | PostgreSQL work samples | sampled timeline | sample | run-independent table state after reset | sample-level |
| settlements | rows | active or terminal settlement rows | PostgreSQL settlement samples | sampled timeline | sample | settlement states | semantics depend on status names |
| queue maximum | work items | max(backlog) | `BACKLOG_TIMELINE.csv` | sampled timeline | sample series | run experiment label | can undercount spikes between samples |
| queue slope | work items/s | delta backlog / delta seconds | `BACKLOG_TIMELINE.csv` | active or drain window | adjacent samples | valid timestamp pairs | noisy with sparse samples |
| message age | seconds | now - earliest queued timestamp | broker samples when available | sampled timeline | queue messages | queue supports age metadata | absent in current local CSV summaries |
| drain start | UTC timestamp | first post-publication sample with backlog > 0 or terminal polling start | scaler logs/timeline | post-load | timestamp | experiment label | approximate if source lacks publication-end timestamp |
| drain end | UTC timestamp | first sample with final backlog zero and terminal state | scaler logs/timeline | drain | timestamp | experiment label | approximate if sampled coarsely |
| drain time | seconds | drain end - drain start, or harness `time_to_drain` | raw result row | drain/processing | seconds | terminal run | may include processing after publication |
| p50 | milliseconds | median latency sample | latency export | processing window | sample count | correlated run samples | source may provide pre-aggregated percentile |
| p95 | milliseconds | nearest-rank p95 latency sample | latency export | processing window | sample count | correlated run samples | weak when sample count is small |
| p99 | milliseconds | nearest-rank p99 latency sample | latency export | processing window | sample count | correlated run samples | weak when sample count is small |
| stable point | events/s | stable capacity on tested grid | `capacity-by-replica.csv` | fixed-topology campaign | offered-rate grid | stability gates | bounded by tested rates |
| knee point | events/s | highest stable point before first unstable or material queue/latency inflection | `capacity-by-replica.csv` | fixed-topology campaign | offered-rate grid | stability gates | currently grid-level, not continuous |
| unstable point | events/s | lowest tested point above stable capacity that fails stability | `capacity-by-replica.csv` | fixed-topology campaign | offered-rate grid | failed stability rows | absent if all tested rates pass |
| speedup | ratio | stable_capacity(n) / stable_capacity(1) | `speedup-efficiency.csv` | fixed-topology campaign | one-replica stable capacity | same grid and gates | invalid if baseline is coarse or incomparable |
| efficiency | ratio | speedup / replica count | `speedup-efficiency.csv` | fixed-topology campaign | replica count | same grid and gates | >1 requires reproduction and explanation |
| marginal gain | events/s | stable_capacity(n) - stable_capacity(n-1) | `speedup-efficiency.csv` | fixed-topology campaign | adjacent replica counts | same grid and gates | grid-limited |
| temporal event loss | events | max(0, publisher-confirmed events - max(inbox rows, accepted events, settlement rows)) | `TEMPORAL_*_RAW_RESULTS.csv` plus per-run correctness export | complete temporal run | publisher-confirmed events | run-correlated rows only | uses the strongest persisted-effect count because some local reset/export paths can retain settlement rows after inbox state changes |
| duplicate effects | rows | duplicate rows detected by correctness query | per-run correctness export and `TEMPORAL_*_RAW_RESULTS.csv` | complete temporal run | run-correlated event IDs | duplicate persisted effects | does not count duplicate delivery attempts unless they produce duplicate persisted effects |
| quarantine count | rows | quarantined rows detected by correctness query | per-run correctness export and `TEMPORAL_*_RAW_RESULTS.csv` | complete temporal run | run-correlated rows | unexpected quarantine rows | expected quarantine must be separated by scenario before comparison |
| CPU-seconds | CPU seconds | process CPU delta across resource samples; if the platform reports zero for a short run, fallback to cpu_avg * replica_seconds | `process-resources.csv`, `TEMPORAL_*_RAW_RESULTS.csv` | process lifetime observed during the run | replica process samples | NatureProtector prevention processes in the experiment | fallback is labelled as an approximation and should not be used for fine-grained efficiency claims |
| replica-seconds | replica seconds | sum(active replica count * elapsed seconds between replica timeline samples) | `replica-timeline.csv`, `TEMPORAL_*_RAW_RESULTS.csv` | temporal run including drain | timeline intervals | experiment label | sampling interval can smooth very short-lived replicas |

## Window Normalization

| Window | Definition | Comparable With |
|---|---|---|
| warm-up | uncounted readiness/build/runtime stabilization before measured workload | warm-up only |
| active load | interval during which the producer publishes configured events | requested, actual and accepted rates |
| steady state | active-load sub-window after startup transients, when backlog slope is near zero | sustained throughput and latency |
| post-load | interval after final publication before terminal state | drain behavior |
| drain | interval until queue and persisted work reach zero | drain time and recovery |
| campaign total | setup plus active load plus drain plus cleanup | operational duration only |
