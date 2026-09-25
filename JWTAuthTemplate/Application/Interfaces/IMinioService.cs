using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Interfaces;

public interface IMinioService
{
    // === Работа с бакетами ===
    Task CreateBucketAsync(string bucketName);

    // === Работа с файлами в MinIO ===
    Task<string> GetObjectETagAsync(string bucketName, string objectName);
    Task<Stream> GetFileAsync(string bucketName, string objectName);
    Task UploadFileAsync(string bucketName, string fileName, Stream dataStream, long length);
    Task<string> GetFileUrlAsync(string bucketName, string objectName);

    // === Работа с документами и версиями ===

    /// <summary>
    /// Загрузить файлы и создать для каждого документ с первой версией
    /// </summary>
    Task<UploadResultDTO> UploadFilesAsync(string bucketName, List<IFormFile> filesData);

    /// <summary>
    /// Получить все документы пользователя
    /// </summary>
    Task<List<FileReferenceDTO>> GetDocumentsByUserIdAsync(string userId);

    /// <summary>
    /// Получить документ по ID
    /// </summary>
    Task<FileReferenceDTO?> GetDocumentByIdAsync(int documentId);

    /// <summary>
    /// Получить все версии документа
    /// </summary>
    Task<List<DocumentVersionDTO>> GetDocumentVersionsAsync(int documentId);

    /// <summary>
    /// Получить версию по ID
    /// </summary>
    Task<DocumentVersionDTO?> GetDocumentVersionByIdAsync(int versionId);

    /// <summary>
    /// Создать новую версию существующего документа
    /// </summary>
    Task<DocumentVersionDTO> CreateNewVersionAsync(int documentId, IFormFile file);

    /// <summary>
    /// Удалить документ и все его версии (из БД и из MinIO)
    /// </summary>
    Task DeleteDocumentAsync(int documentId);

    /// <summary>
    /// Удалить конкретную версию документа (из БД и из MinIO)
    /// </summary>
    Task DeleteDocumentVersionAsync(int versionId);

    // === Парсинг содержимого файлов ===
    Task<string> GetExcelFileContentAsJson(string bucketName, string objectName);
    Task<string> GetExcelFileContentAsJsonWithLimits(string bucketName, string objectName, double inputX1, double inputX2, double inputY1, double inputY2);
    Task<string> GetSPCFileContentAsJson(string bucketName, string objectName);
}