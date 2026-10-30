namespace JWTAuthTemplate.Application.Interfaces;
public interface IObjectStorage
{
    Task Put(string bucket, string name, Stream stream, long length, CancellationToken ct);
    Task<byte[]> Read(string bucket, string name, CancellationToken ct);
    Task<string> ETag(string bucket, string name, CancellationToken ct);
    Task Delete(string bucket, string name, CancellationToken ct);
}
