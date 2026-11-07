using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Документы пользователя и их версии (метаданные в БД, содержимое в объектном хранилище).</summary>
public interface IDocumentService
{
    /// <summary>
    /// Загружает файлы; для каждого создаётся документ с первой версией.
    /// Если <paramref name="allowDuplicateNames"/> = false, повтор имени файла у пользователя — конфликт.
    /// </summary>
    Task<UploadResultDTO> UploadFilesAsync(string userId, IReadOnlyList<FileUpload> files, bool allowDuplicateNames = false);

    Task<List<FileReferenceDTO>> GetDocumentsAsync(string userId);
    Task<FileReferenceDTO> GetDocumentAsync(int documentId);
    Task DeleteDocumentAsync(int documentId);

    Task<List<DocumentVersionDTO>> GetVersionsAsync(int documentId);
    Task<DocumentVersionDTO> GetVersionAsync(int versionId);
    Task<DocumentVersionDTO> CreateVersionAsync(int documentId, FileUpload file);
    Task DeleteVersionAsync(int versionId);
    Task<(Stream Content, string FileName)> OpenVersionAsync(int versionId);
}
