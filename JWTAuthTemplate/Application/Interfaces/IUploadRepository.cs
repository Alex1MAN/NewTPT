using JWTAuthTemplate.Application.Models;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Порт хранилища сессий загрузки по частям.</summary>
public interface IUploadRepository
{
    /// <summary>Открывает транзакцию с эксклюзивной блокировкой по ключу сессии.</summary>
    Task<IUploadTransaction> Begin(string key, CancellationToken ct);
}

public interface IUploadTransaction : IAsyncDisposable
{
    Task<ChunkUploadSession?> Get(string key, CancellationToken ct);
    Task<bool> UserExists(string userId, CancellationToken ct);
    void Add(ChunkUploadSession session);

    /// <summary>Создаёт документ с первой версией для собранного файла и возвращает Id документа.</summary>
    Task<int> AddDocument(ChunkUploadSession session, long length, string etag, CancellationToken ct);

    Task Commit(CancellationToken ct);
}
