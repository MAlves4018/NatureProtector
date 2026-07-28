# Scalability Experiment Protocol

Canonical local evidence is produced by `scripts/testing/Invoke-AutoscalingExperimentMatrix.ps1` and consolidated by `scripts/autoscaling/compile-scalability-report.py`.

The current live campaign samples RabbitMQ, PostgreSQL work state, replica count, latency and correctness for S1-S8 autoscaling scenarios. Results are local observed capacity only.

Open protocol gap: repeated fixed 1/2/3/4 replica capacity curves are not yet complete.
