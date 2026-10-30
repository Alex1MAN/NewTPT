# CI-проверка того же исходного MinIO, собранного снаружи Docker.
FROM scratch
COPY artifacts/minio /minio
COPY artifacts/minio-source.tar.gz /usr/share/minio/source.tar.gz
EXPOSE 9000 9001
ENTRYPOINT ["/minio"]
CMD ["server", "/data", "--console-address", ":9001"]
