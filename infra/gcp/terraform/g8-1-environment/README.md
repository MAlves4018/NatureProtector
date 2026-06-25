# G8.1 environment root

Reusable root for staging or production in separate new GCP projects. It defines a private VPC, regional Autopilot cluster, Cloud SQL regional HA with PITR, Secret Manager containers and the external Application Load Balancer/Cloud Armor edge.

The root is disabled by default. Cloud Run service revisions remain owned by Cloud Deploy; the edge references their stable service names and forces public traffic through Cloud Armor by requiring Cloud Run ingress `internal-and-cloud-load-balancing` in the deployment manifests.


## Remote state

Initialise this reusable root against the protected platform GCS bucket with a unique prefix per environment. Never share a prefix between staging and production. The one-week teardown changes this same state; it must not run from a fresh local state.
