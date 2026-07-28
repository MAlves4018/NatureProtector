# Scalability Results

Source: `artifacts/scalability-final/SCALABILITY-SUMMARY.json`.

Current fixed-replica campaign:

- 1 replica: stable 0.5 events/s; knee 0.5; first unstable 1.0.
- 2 replicas: stable 1.5 events/s; knee 1.5; first unstable 3.0; speedup 3.0; efficiency 1.5.
- 3 replicas: stable 1.5 events/s; knee 1.5; first unstable 3.0; speedup 3.0; efficiency 1.0.
- 4 replicas: stable 1.5 events/s; knee 1.5; first unstable 3.0; speedup 3.0; efficiency 0.75.

Current final autoscaling comparison:

- Final autoscaling campaign passed.
- Best autoscaling row processed 3.285 events/s, scaled to four replicas and returned to one replica with final backlog zero.
