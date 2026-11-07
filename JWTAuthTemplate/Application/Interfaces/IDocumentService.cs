using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Документы, их версии и низкоуровневые операции с файлами в хранилище.</summary>
public interface IDocumentService
{
    Task CreateBucketAsync(string bucketName);

    /// <summary>Загружает файлы; для каждого создаётся документ с первой версией.</summary>
    Task<UploadResultDTO> UploadFilesAsync(string bucketName, IReadOnlyList<FileUpload> files);

    /// <summary>Проверяет, что имена файлов не повторяются и ещё не заняты документами пользователя.</summary>
    Task EnsureUniqueFilesAsync(string bucketName, IReadOnlyList<string> fileNames);

    /// <summary>Кладёт файл в хранилище без создания документа.</summary>
    Task PutObjectAsync(string bucketName, string objectName, FileUpload file);

    Task<List<FileReferenceDTO>> GetDocumentsAsync(string userId);
    Task<FileReferenceDTO> GetDocumentAsync(int documentId);
    Task DeleteDocumentAsync(int documentId);

    Task<List<DocumentVersionDTO>> GetVersionsAsync(int documentId);
    Task<DocumentVersionDTO> GetVersionAsync(int versionId);
    Task<DocumentVersionDTO> CreateVersionAsync(int documentId, FileUpload file);
    Task DeleteVersionAsync(int versionId);
    Task<(Stream Content, string FileName)> OpenVersionAsync(int versionId);

    Task<Stream> OpenObjectAsync(string bucketName, string objectName);
    Task<string> GetObjectUrlAsync(string bucketName, string objectName);

    Task AddReferenceAsync(UserReferencesInMinio reference);
    Task<List<UserReferencesInMinio>> GetReferencesAsync(string userId);
}
