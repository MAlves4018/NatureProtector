FROM otel/opentelemetry-collector-contrib@sha256:4935caa35e9a4cb387e35732e8fb22b2b5759af8d12e7043357f03837f6e8df5 AS collector

FROM alpine:3.22
RUN apk add --no-cache ca-certificates
COPY --from=collector /otelcol-contrib /otelcol-contrib
COPY infra/gcp/otel/config.yaml /etc/otelcol-google/config.yaml
USER 65532:65532
ENTRYPOINT ["/otelcol-contrib"]
CMD ["--config=/etc/otelcol-google/config.yaml"]
EXPOSE 4317 4318 13133
