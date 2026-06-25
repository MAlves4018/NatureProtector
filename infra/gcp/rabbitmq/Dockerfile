FROM rabbitmq:4.3.2-management-alpine
COPY infra/gcp/rabbitmq/rabbitmq.conf /etc/rabbitmq/rabbitmq.conf
COPY infra/gcp/rabbitmq/enabled_plugins /etc/rabbitmq/enabled_plugins
HEALTHCHECK --interval=10s --timeout=5s --retries=12 CMD rabbitmq-diagnostics -q ping
USER rabbitmq
EXPOSE 5672 15672 15692
