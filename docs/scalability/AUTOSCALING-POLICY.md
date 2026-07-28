# Autoscaling Policy

The local policy combines RabbitMQ queue depth with PostgreSQL pipeline work. It scales up when work exceeds `TargetBacklogPerReplica * activeReplicas` and scales down to the minimum after observed drain.

Candidate V1 config is stored in `config/autoscaling/capacity-experiments.json`. These values are local experiment parameters, not production calibration.
