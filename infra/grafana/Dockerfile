FROM grafana/grafana-enterprise:13.0.1-ubuntu
ARG INFINITY_PLUGIN_VERSION=3.6.0
RUN grafana cli --pluginsDir /var/lib/grafana/plugins plugins install yesoreyeram-infinity-datasource ${INFINITY_PLUGIN_VERSION}
