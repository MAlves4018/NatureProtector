# syntax=docker/dockerfile:1.7
FROM alpine:3.22
RUN apk add --no-cache ca-certificates
ADD --checksum=sha256:1e9045ec32bea85da43de85f0065358529ea7c7a152eca78154fba5b58c27d82 \
    https://dl.k8s.io/release/v1.36.2/bin/linux/amd64/kubectl /bin/kubectl
RUN chmod 0555 /bin/kubectl
ENV HOME=/tmp
USER 65532:65532
ENTRYPOINT ["/bin/kubectl"]
