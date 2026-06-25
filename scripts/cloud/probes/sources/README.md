# G8.2 runtime source adapters

This directory is intentionally fail-closed in the pre-G10 integration package.
Each action adapter must be implemented against the actual post-G10 staging
endpoints and evidence outputs. An adapter must derive `raw-probe-source.json`
from runtime APIs, Cloud Monitoring, Cloud Audit Logs, Billing export or a
separately attested operations workflow. It may not accept a manually authored
measurement JSON or an arbitrary command from `workflow_dispatch`.

The G8.2 integrity chain, schemas, aggregation, archive, review and authorization
are implemented. Runtime adapter integration is a declared G9/G10 owner gate and
cannot be counted as runtime proof until the new GCP projects exist.
