# syntax=docker/dockerfile:1.7
ARG ALPINE_IMAGE=alpine:3.22
FROM ${ALPINE_IMAGE}
RUN apk add --no-cache curl jq ca-certificates \
    && addgroup -S smoke \
    && adduser -S -D -H -G smoke smoke
WORKDIR /opt/natureprotector
COPY --chown=smoke:smoke infra/gcp/smoke/smoke.sh ./smoke.sh
RUN chmod 0555 ./smoke.sh
USER smoke
ENTRYPOINT ["/bin/sh", "/opt/natureprotector/smoke.sh"]
