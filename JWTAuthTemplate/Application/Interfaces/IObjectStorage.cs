namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Порт объектного хранилища (реализация — MinIO в слое Infrastructure).</summary>
public interface IObjectStorage
{
    /// <summary>Создаёт бакет, если его ещё нет.</summary>
    Task EnsureBucketAsync(string bucket, CancellationToken ct = default);

    Task PutAsync(string bucket, string objectName, Stream data, long length, CancellationToken ct = default);

    /// <summary>Читает объект целиком; при превышении <paramref name="maxBytes"/> — ArgumentException.</summary>
    Task<byte[]> ReadAllBytesAsync(string bucket, string objectName, long? maxBytes = null, CancellationToken ct = default);

    Task<string> GetETagAsync(string bucket, string objectName, CancellationToken ct = default);

    Task DeleteAsync(string bucket, string objectName, CancellationToken ct = default);

    /// <summary>Временная (1 час) ссылка на скачивание объекта.</summary>
    Task<string> GetPresignedUrlAsync(string bucket, string objectName);
}
