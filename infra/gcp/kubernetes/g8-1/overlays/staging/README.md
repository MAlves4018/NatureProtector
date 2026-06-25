# Staging qualification profile

This overlay is deliberately **non-production** and ephemeral. It preserves the
same images, protocols, TLS, PostgreSQL schema, RabbitMQ contracts and OTLP
instrumentation as production, while reducing replica counts and resource
requests for the first bounded deployment proof.

It must not be used to claim high availability, production capacity, quorum
resilience, seasonal stability or production authorization. Production keeps
the separate `overlays/production` profile and the production Cloud SQL guardrails.

Before applying this profile:

1. produce a cost estimate;
2. confirm the staging project and billing association;
3. keep `create_data_plane=false` until the owner gate passes;
4. rehearse teardown in the same session;
5. record all active resources after deployment and after cleanup.
