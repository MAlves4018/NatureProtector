FROM python:3.13.5-alpine3.22
WORKDIR /app
COPY scripts/common/np_line_protocol.py /app/scripts/common/np_line_protocol.py
COPY scripts/data/poll-ipma-observations.py /app/scripts/data/poll-ipma-observations.py
COPY scripts/observability/collect-operational-timeseries.py /app/scripts/observability/collect-operational-timeseries.py
COPY config/external-data/ipma.json /app/config/external-data/ipma.json
ENV PYTHONUNBUFFERED=1
CMD ["python", "/app/scripts/data/poll-ipma-observations.py", "--config", "/app/config/external-data/ipma.json", "--state", "/data/cursor.json", "--output", "/data/latest.lineprotocol", "--write-influx"]
