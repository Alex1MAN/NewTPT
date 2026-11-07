using JWTAuthTemplate.Application.Interfaces;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace JWTAuthTemplate.Infrastructure.Storage;

/// <summary>Единственная реализация объектного хранилища поверх MinIO.</summary>
public sealed class MinioObjectStorage : IObjectStorage
{
    private const int PresignedUrlLifetimeSeconds = 3600;
    private readonly IMinioClient _client;
    private readonly MinioSettings _settings;

    public MinioObjectStorage(IMinioClient client, IOptions<MinioSettings> settings)
        => (_client, _settings) = (client, settings.Value);

    public async Task EnsureBucketAsync(string bucket, CancellationToken ct = default)
    {
        if (!await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct))
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
    }

    public async Task PutAsync(string bucket, string objectName, Stream data, long length, CancellationToken ct = default)
        => await _client.PutObjectAsync(new PutObjectArgs().WithBucket(bucket).WithObject(objectName)
            .WithStreamData(data).WithObjectSize(length).WithContentType("application/octet-stream"), ct);

    public async Task<byte[]> ReadAllBytesAsync(string bucket, string objectName, long? maxBytes = null, CancellationToken ct = default)
    {
        if (maxBytes.HasValue)
        {
            // Быстрый отказ по метаданным до скачивания.
            var stat = await _client.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(objectName), ct);
            if (stat.Size > maxBytes.Value)
                throw new ArgumentException("File exceeds size limit.");
        }
        using var result = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs().WithBucket(bucket).WithObject(objectName)
            .WithCallbackStream(async (stream, token) =>
            {
                var buffer = new byte[81920];
                int count;
                while ((count = await stream.ReadAsync(buffer, token)) > 0)
                {
                    // Повторная проверка на случай подмены объекта между Stat и Get.
                    if (maxBytes.HasValue && result.Length + count > maxBytes.Value)
                        throw new ArgumentException("File exceeds size limit.");
                    await result.WriteAsync(buffer.AsMemory(0, count), token);
                }
            }), ct);
        return result.ToArray();
    }

    public async Task<string> GetETagAsync(string bucket, string objectName, CancellationToken ct = default)
        => (await _client.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(objectName), ct)).ETag;

    public async Task DeleteAsync(string bucket, string objectName, CancellationToken ct = default)
        => await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(objectName), ct);

    public async Task<string> GetPresignedUrlAsync(string bucket, string objectName)
    {
        var args = new PresignedGetObjectArgs().WithBucket(bucket).WithObject(objectName).WithExpiry(PresignedUrlLifetimeSeconds);
        if (string.IsNullOrWhiteSpace(_settings.PublicEndpoint))
            return await _client.PresignedGetObjectAsync(args);

        // Подпись должна совпадать с адресом, который увидит клиент, поэтому используется публичный endpoint.
        // Регион задан явно, чтобы клиент не обращался к MinIO по публичному адресу изнутри контейнера.
        using var publicClient = new MinioClient().WithEndpoint(_settings.PublicEndpoint)
            .WithCredentials(_settings.AccessKey, _settings.SecretKey).WithRegion("us-east-1").Build();
        return await publicClient.PresignedGetObjectAsync(args);
    }
}
