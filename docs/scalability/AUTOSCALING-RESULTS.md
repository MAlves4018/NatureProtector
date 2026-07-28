# Autoscaling Results

Source campaign: `artifacts/acceptance/matrices/autoscaling-runtime/20260727T110004Z`.

Result: `AUTOSCALING_REALTIME_OBSERVABILITY_PROVED`.

The campaign executed S1-S8, scaled Prevention.Host from one to four replicas, drained backlog to zero and ended at one replica.

Best final autoscaling row: S8 processed 3.285 events/s with peak backlog 186 and final backlog zero.
