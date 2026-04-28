# NatureProtector.AppHost

Use `docker-compose.yml` when you want the current baseline exactly as provisioned today, including existing bind mounts and infra provisioning under `infra/`.

Use the Aspire AppHost when you want a development cockpit with local process orchestration, dashboard visibility over processes, traces, metrics, logs, and environment inspection in one place.

Current limitations:

- it is additive and does not replace `docker-compose.yml`
- it targets local developer feedback, not production topology
- it does not yet mirror every compose mount and provisioning detail
- PostgreSQL bootstrap remains a separate manual step
