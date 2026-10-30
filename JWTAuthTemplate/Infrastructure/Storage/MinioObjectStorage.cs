using JWTAuthTemplate.Application.Interfaces;
using Minio;
using Minio.DataModel.Args;
namespace JWTAuthTemplate.Infrastructure.Storage;
public sealed class MinioObjectStorage : IObjectStorage
{
    private readonly IMinioClient _client;
    public MinioObjectStorage(IMinioClient client) => _client = client;
    public async Task Put(string bucket, string name, Stream stream, long length, CancellationToken ct)
        => await _client.PutObjectAsync(new PutObjectArgs().WithBucket(bucket).WithObject(name)
            .WithStreamData(stream).WithObjectSize(length).WithContentType("application/octet-stream"), ct);
    public async Task<byte[]> Read(string bucket, string name, CancellationToken ct)
    {
        using var data = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs().WithBucket(bucket).WithObject(name)
            .WithCallbackStream(async (stream, token) =>
            {
                var buffer = new byte[81920];
                int count;
                while ((count = await stream.ReadAsync(buffer, token)) > 0)
                {
                    if (data.Length + count > Application.Services.ChunkUploadService.MaxChunkBytes)
                        throw new IOException("Stored chunk exceeds size limit.");
                    await data.WriteAsync(buffer.AsMemory(0, count), token);
                }
            }), ct);
        return data.ToArray();
    }
    public async Task<string> ETag(string bucket, string name, CancellationToken ct)
        => (await _client.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(name), ct)).ETag;
    public async Task Delete(string bucket, string name, CancellationToken ct)
        => await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(name), ct);
}
