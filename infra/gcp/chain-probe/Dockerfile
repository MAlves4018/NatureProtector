FROM alpine:3.22
RUN apk add --no-cache bash curl jq ca-certificates
COPY infra/gcp/chain-probe/probe.sh /probe.sh
RUN chmod 0555 /probe.sh
USER 65532:65532
ENTRYPOINT ["/probe.sh"]
