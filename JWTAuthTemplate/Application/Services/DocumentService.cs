using JWTAuthTemplate.Application.Exceptions;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace JWTAuthTemplate.Application.Services;

/// <summary>Сценарии работы с документами и их версиями; содержимое хранится в объектном хранилище.</summary>
public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _repository;
    private readonly IObjectStorage _storage;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(IDocumentRepository repository, IObjectStorage storage, ILogger<DocumentService> logger)
        => (_repository, _storage, _logger) = (repository, storage, logger);

    public async Task<UploadResultDTO> UploadFilesAsync(string userId, IReadOnlyList<FileUpload> files, bool allowDuplicateNames = false)
    {
        if (string.IsNullOrWhiteSpace(userId) || files == null || files.Count == 0 ||
            files.Any(f => f == null || f.Length == 0))
            throw new ArgumentException("No files provided for upload");
        if (!await _repository.UserExistsAsync(userId))
            throw new KeyNotFoundException("User not found.");
        if (!allowDuplicateNames)
            await EnsureUniqueNamesAsync(userId, files.Select(f => f.FileName).ToList());

        var result = new UploadResultDTO { BucketName = userId };
        await using var transaction = await _repository.BeginTransactionAsync();
        foreach (var file in files)
        {
            var document = await _repository.AddDocumentAsync(userId, file.FileName);
            var version = await StoreVersionAsync(document, 1, file);
            result.References.Add(ToDto(document, version));
        }
        result.Count = result.References.Count;
        await transaction.CommitAsync();
        return result;
    }

    /// <summary>Имена не должны повторяться ни между собой, ни с существующими документами пользователя.</summary>
    private async Task EnsureUniqueNamesAsync(string userId, IReadOnlyList<string> fileNames)
    {
        var names = (await _repository.GetDocumentsByUserAsync(userId))
            .Select(d => d.OriginalName).ToHashSet(StringComparer.Ordinal);
        if (fileNames.Any(n => !names.Add(n)))
            throw new ConflictException("File already exists. Rename the file or create a new version.");
    }

    public async Task<List<FileReferenceDTO>> GetDocumentsAsync(string userId)
        => (await _repository.GetDocumentsByUserAsync(userId)).Select(d => ToDto(d, d.CurrentVersion)).ToList();

    public async Task<FileReferenceDTO> GetDocumentAsync(int documentId)
    {
        var document = await FindDocumentAsync(documentId);
        return ToDto(document, document.CurrentVersion);
    }

    public async Task DeleteDocumentAsync(int documentId)
    {
        var document = await FindDocumentAsync(documentId, includeVersions: true);
        foreach (var version in document.Versions)
            await DeleteObjectQuietlyAsync(document.UserId, version.MinioObjectName);
        await _repository.DeleteDocumentAsync(document);
    }

    public async Task<List<DocumentVersionDTO>> GetVersionsAsync(int documentId)
        => (await _repository.GetVersionsAsync(documentId)).Select(ToDto).ToList();

    public async Task<DocumentVersionDTO> GetVersionAsync(int versionId) => ToDto(await FindVersionAsync(versionId));

    public async Task<DocumentVersionDTO> CreateVersionAsync(int documentId, FileUpload file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is required.");
        var document = await FindDocumentAsync(documentId, includeVersions: true);
        var extension = ExtensionOf(file.FileName);
        if (!string.Equals(document.FileExtension, extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"File extension mismatch. Expected: {document.FileExtension}, got: {extension}");

        int number = document.Versions.Select(v => v.VersionNumber).DefaultIfEmpty(0).Max() + 1;
        return ToDto(await StoreVersionAsync(document, number, file));
    }

    public async Task DeleteVersionAsync(int versionId)
    {
        var version = await FindVersionAsync(versionId);
        if (await _repository.CountVersionsAsync(version.DocumentId) == 1)
            throw new ConflictException("Cannot delete the last version of a document. Delete the entire document instead.");
        await DeleteObjectQuietlyAsync(version.Document.UserId, version.MinioObjectName);
        await _repository.DeleteVersionAsync(version);
    }

    public async Task<(Stream Content, string FileName)> OpenVersionAsync(int versionId)
    {
        var version = await FindVersionAsync(versionId);
        var bytes = await _storage.ReadAllBytesAsync(version.Document.UserId, version.MinioObjectName);
        return (new MemoryStream(bytes, writable: false), version.OriginalFileName);
    }

    /// <summary>Кладёт файл в MinIO по пути {userId}/{documentId}/v{N}_{имя} и сохраняет версию.</summary>
    private async Task<DocumentVersion> StoreVersionAsync(Document document, int number, FileUpload file)
    {
        var objectName = $"{document.UserId}/{document.Id}/v{number}_{file.FileName}";
        await using (var stream = file.OpenReadStream())
            await _storage.PutAsync(document.UserId, objectName, stream, file.Length);
        var etag = await _storage.GetETagAsync(document.UserId, objectName);
        return await _repository.AddVersionAsync(document, number, objectName, etag, file.Length, file.FileName);
    }

    private async Task<Document> FindDocumentAsync(int documentId, bool includeVersions = false)
        => await _repository.GetDocumentAsync(documentId, includeVersions)
           ?? throw new KeyNotFoundException($"Document with ID {documentId} not found");

    private async Task<DocumentVersion> FindVersionAsync(int versionId)
        => await _repository.GetVersionAsync(versionId)
           ?? throw new KeyNotFoundException($"Version with ID {versionId} not found");

    /// <summary>Ошибка удаления объекта не прерывает удаление записи в БД, но логируется.</summary>
    private async Task DeleteObjectQuietlyAsync(string bucket, string objectName)
    {
        try { await _storage.DeleteAsync(bucket, objectName); }
        catch (Exception ex) { _logger.LogWarning(ex, "Не удалось удалить объект {Object} из MinIO", objectName); }
    }

    public static string ExtensionOf(string fileName) => Path.GetExtension(fileName).TrimStart('.');

    private static FileReferenceDTO ToDto(Document d, DocumentVersion? v) => new()
    {
        DocumentId = d.Id,
        FileName = d.OriginalName,
        FileReferenceMinio = v?.ETag ?? "",
        MinioObjectName = v?.MinioObjectName ?? "",
        FileExtension = d.FileExtension,
        CurrentVersionNumber = v?.VersionNumber ?? 0,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt
    };

    private static DocumentVersionDTO ToDto(DocumentVersion v) => new()
    {
        VersionId = v.Id,
        DocumentId = v.DocumentId,
        VersionNumber = v.VersionNumber,
        MinioObjectName = v.MinioObjectName,
        FileSize = v.FileSize,
        OriginalFileName = v.OriginalFileName,
        CreatedAt = v.CreatedAt
    };
}
