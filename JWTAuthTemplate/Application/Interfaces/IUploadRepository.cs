using JWTAuthTemplate.Application.Models;
namespace JWTAuthTemplate.Application.Interfaces;
public interface IUploadRepository
{
    Task<IUploadTransaction> Begin(string key, CancellationToken ct);
}
public interface IUploadTransaction : IAsyncDisposable
{
    Task<ChunkUploadSession?> Get(string key, CancellationToken ct);
    Task<bool> UserExists(string userId, CancellationToken ct);
    void Add(ChunkUploadSession session);
    Task<int> AddDocument(ChunkUploadSession session, long length, string etag, CancellationToken ct);
    Task Commit(CancellationToken ct);
}
