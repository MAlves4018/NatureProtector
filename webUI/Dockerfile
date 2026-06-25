# syntax=docker/dockerfile:1.7
ARG NODE_IMAGE=node:22.17.1-alpine
ARG NGINX_IMAGE=nginx:1.29-alpine-slim

FROM ${NODE_IMAGE} AS build
WORKDIR /src
COPY webUI/package.json webUI/package-lock.json ./
RUN npm ci --ignore-scripts
COPY webUI/ ./
COPY .github/workflows/engineering-foundations.yml \
     .github/workflows/release-candidate.yml \
     .github/workflows/security.yml \
     /.github/workflows/
RUN npm run check:toolchain && npm run build

FROM ${NGINX_IMAGE} AS runtime
RUN apk upgrade --no-cache openssl libcrypto3 libssl3
COPY --chown=nginx:nginx webUI/nginx.template.conf /opt/natureprotector/nginx.template.conf
COPY --chown=nginx:nginx webUI/nginx-main.conf /opt/natureprotector/nginx-main.conf
COPY --chown=nginx:nginx webUI/container-entrypoint.sh /opt/natureprotector/container-entrypoint.sh
COPY --from=build --chown=nginx:nginx /src/dist/ /usr/share/nginx/html/
USER nginx
EXPOSE 8080
STOPSIGNAL SIGQUIT
ENTRYPOINT ["/bin/sh", "/opt/natureprotector/container-entrypoint.sh"]
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/healthz >/dev/null || exit 1
