using JWTAuthTemplate.Application.Models;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Порт хранилища документов, версий и legacy-ссылок на файлы.</summary>
public interface IDocumentRepository
{
    Task<bool> UserExistsAsync(string userId);

    /// <summary>Открывает транзакцию БД; без вызова CommitAsync изменения откатываются.</summary>
    Task<IUnitOfWork> BeginTransactionAsync();

    /// <summary>Сохраняет новый документ и возвращает его с назначенным Id.</summary>
    Task<Document> AddDocumentAsync(string userId, string fileName);

    /// <summary>Сохраняет версию и делает её текущей версией документа.</summary>
    Task<DocumentVersion> AddVersionAsync(Document document, int versionNumber, string objectName,
        string etag, long fileSize, string fileName);

    /// <summary>Добавляет legacy-ссылку UserReferencesInMinio для загруженного документа.</summary>
    Task AddLegacyReferenceAsync(Document document, string etag);

    Task<List<Document>> GetDocumentsByUserAsync(string userId);
    Task<Document?> GetDocumentAsync(int documentId, bool includeVersions = false);
    Task<List<DocumentVersion>> GetVersionsAsync(int documentId);
    Task<DocumentVersion?> GetVersionAsync(int versionId);
    Task<int> CountVersionsAsync(int documentId);

    /// <summary>Удаляет документ со всеми версиями в одной транзакции.</summary>
    Task DeleteDocumentAsync(Document document);

    /// <summary>Удаляет версию; если она текущая, текущей становится предыдущая.</summary>
    Task DeleteVersionAsync(DocumentVersion version);

    Task AddReferenceAsync(UserReferencesInMinio reference);
    Task<List<UserReferencesInMinio>> GetReferencesAsync(string userId);
}

/// <summary>Транзакция, которую нужно явно подтвердить.</summary>
public interface IUnitOfWork : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
}
