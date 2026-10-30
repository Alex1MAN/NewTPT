# Резервная воспроизводимая сборка исходной версии MinIO.
# Лицензия сервера MinIO: AGPL-3.0, исходники включены в образ.
FROM golang:1.21.6-bookworm AS build
WORKDIR /src
ADD https://codeload.github.com/minio/minio/tar.gz/refs/tags/RELEASE.2024-01-18T22-51-28Z /tmp/minio.tar.gz
RUN echo "267c949e03f6d931f3b2da07c067f02483cb2b2604b627e1eb9b96acb0fd9192  /tmp/minio.tar.gz" | sha256sum -c - \
    && tar -xzf /tmp/minio.tar.gz --strip-components=1
ENV CGO_ENABLED=0 GOMAXPROCS=2
RUN go build -p 2 -trimpath -tags kqueue,netgo -ldflags="-s -w" -o /minio .
FROM scratch
COPY --from=build /minio /minio
COPY --from=build /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
COPY --from=build /src/LICENSE /LICENSE
COPY --from=build /tmp/minio.tar.gz /usr/share/minio/source.tar.gz
EXPOSE 9000 9001
ENTRYPOINT ["/minio"]
CMD ["server", "/data", "--console-address", ":9001"]
