# Temporal Workload Protocol

The authoritative workload catalog is `config/autoscaling/temporal-workloads.json`.

Required workloads:

- `W1-low-constant`
- `W2-near-knee-constant`
- `W3-sustained-overload`
- `W4-short-spike`
- `W5-step-load`
- `W6-ramp-up`
- `W7-rise-hold-fall`

The comparison protocol runs the same workload definitions across:

- `fixed-one`
- `best-fixed` using the `-BestFixedReplicas` harness parameter
- `autoscaling`

The complete matrix is seven workloads times three topologies times three repetitions. The compiler rejects fewer than 63 valid rows.

The best fixed topology must come from the capacity-refinement evidence, not from a hardcoded assumption. In the current local S16-R1 run, three replicas are the conservative choice because four replicas reproduced a correction failure at 2.90 events/s in repetition 2.

Windows are separated as configured warm-up, active workload duration, segment windows, drain timeout and campaign total. Sustained throughput must be calculated from the comparable active/drain evidence, not from an arbitrary wall-clock interval that includes unrelated idle time.
