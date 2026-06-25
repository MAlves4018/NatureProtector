# G8.1 Cloud Deploy configuration

Three independent pipelines promote the exact API, frontend and Prevention image digests from staging to production. Cloud Run uses automatic traffic control for canary phases. Prevention uses GKE service-networking canaries. Migrations, bootstrap and Simulator remain explicit jobs and are never canaried.
