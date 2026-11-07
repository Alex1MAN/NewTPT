using JWTAuthTemplate.Application.Models;

namespace JWTAuthTemplate.Infrastructure.Database;

/// <summary>
/// Общая запись документа, версии и legacy-ссылки. Используется и обычной загрузкой,
/// и загрузкой по частям, чтобы правила создания документа были в одном месте.
/// </summary>
internal static class DocumentWriter
{
    public static async Task<Document> AddDocumentAsync(Context context, string userId, string fileName, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var document = new Document
        {
            UserId = userId,
            OriginalName = fileName,
            FileExtension = Path.GetExtension(fileName).TrimStart('.'),
            CreatedAt = now,
            UpdatedAt = now
        };
        context.Documents.Add(document);
        await context.SaveChangesAsync(ct);
        return document;
    }

    /// <summary>Сохраняет версию и делает её текущей (CurrentVersionId сохраняется следующим SaveChanges).</summary>
    public static async Task<DocumentVersion> AddVersionAsync(Context context, Document document, int versionNumber,
        string objectName, string etag, long fileSize, string fileName, CancellationToken ct = default)
    {
        var version = new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = versionNumber,
            MinioObjectName = objectName,
            ETag = etag,
            FileSize = fileSize,
            OriginalFileName = fileName,
            CreatedAt = DateTime.UtcNow
        };
        context.DocumentVersions.Add(version);
        await context.SaveChangesAsync(ct);
        document.CurrentVersionId = version.Id;
        document.UpdatedAt = version.CreatedAt;
        return version;
    }

    public static void AddLegacyReference(Context context, Document document, string etag)
        => context.UserReferencesInMinio.Add(new UserReferencesInMinio
        {
            UserId = document.UserId,
            FileName = document.OriginalName,
            FileExtension = document.FileExtension,
            FileReferenceMinio = etag
        });
}
