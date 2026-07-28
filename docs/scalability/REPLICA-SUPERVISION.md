# Replica Supervision

`Invoke-AutoscalingExperimentMatrix.ps1` starts local Prevention.Host processes with unique `Replica__InstanceId`, tracks process handles, redirects logs and removes campaign-owned replicas in `finally`.

Scale-up is considered active only after the process is running and RabbitMQ/backlog samples reflect live work. Scale-down is verified from the final replica timeline sample.
