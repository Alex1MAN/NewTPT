using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

[ApiController]
[Route("minio")]
public class MinioController : BaseController
{
    private readonly IMinioService _minioService;

    public MinioController(IMinioService minioService)
    {
        _minioService = minioService;
    }

    #region Бакеты

    [HttpPost("create-bucket")]
    public async Task<IActionResult> CreateBucket(string bucketName)
    {
        return await ExecuteSafeAsync(async () =>
        {
            await _minioService.CreateBucketAsync(bucketName);
            return Ok($"Bucket {bucketName} created");
        });
    }

    #endregion

    #region Загрузка файлов (создание документов)

    /// <summary>
    /// Загрузить файлы. Для каждого файла создаётся документ с первой версией.
    /// </summary>
    [HttpPost("upload-files")]
    public async Task<IActionResult> UploadFiles(string bucketName, [FromForm] List<IFormFile> filesData)
    {
        return await ExecuteSafeAsync(async () =>
        {
            var result = await _minioService.UploadFilesAsync(bucketName, filesData);
            return Ok(result);
        });
    }

    /// <summary>
    /// Низкоуровневая загрузка одного файла в MinIO (без создания документа)
    /// </summary>
    [HttpPost("upload-file")]
    public async Task<IActionResult> UploadFile(string bucketName, string objectName, IFormFile file)
    {
        return await ExecuteSafeAsync(async () =>
        {
            using var stream = file.OpenReadStream();
            await _minioService.UploadFileAsync(bucketName, objectName, stream, file.Length);
            return Ok($"File {objectName} uploaded to bucket {bucketName}");
        });
    }

    #endregion

    #region Документы

    /// <summary>
    /// Получить все документы пользователя
    /// </summary>
    [HttpGet("documents/{userId}")]
    public async Task<IActionResult> GetDocuments(string userId)
    {
        return await ExecuteSafeAsync(async () =>
        {
            var documents = await _minioService.GetDocumentsByUserIdAsync(userId);
            return Ok(documents);
        });
    }

    /// <summary>
    /// Получить документ по ID
    /// </summary>
    [HttpGet("documents/detail/{documentId}")]
    public async Task<IActionResult> GetDocument(int documentId)
    {
        return await ExecuteSafeAsync(async () =>
        {
            var document = await _minioService.GetDocumentByIdAsync(documentId);
            if (document == null)
                return NotFound($"Document with ID {documentId} not found");
            return Ok(document);
        });
    }

    /// <summary>
    /// Удалить документ и все его версии
    /// </summary>
    [HttpDelete("documents/{documentId}")]
    public async Task<IActionResult> DeleteDocument(int documentId)
    {
        return await ExecuteSafeAsync(async () =>
        {
            await _minioService.DeleteDocumentAsync(documentId);
            return Ok($"Document {documentId} and all its versions deleted");
        });
    }

    #endregion

    #region Версии документов

    /// <summary>
    /// Получить историю всех версий документа
    /// </summary>
    [HttpGet("documents/{documentId}/versions")]
    public async Task<IActionResult> GetDocumentVersions(int documentId)
    {
        return await ExecuteSafeAsync(async () =>
        {
            var versions = await _minioService.GetDocumentVersionsAsync(documentId);
            return Ok(versions);
        });
    }

    /// <summary>
    /// Получить информацию о конкретной версии
    /// </summary>
    [HttpGet("documents/versions/{versionId}")]
    public async Task<IActionResult> GetDocumentVersion(int versionId)
    {
        return await ExecuteSafeAsync(async () =>
        {
            var version = await _minioService.GetDocumentVersionByIdAsync(versionId);
            if (version == null)
                return NotFound($"Version with ID {versionId} not found");
            return Ok(version);
        });
    }

    /// <summary>
    /// Создать новую версию существующего документа
    /// </summary>
    [HttpPost("documents/{documentId}/versions")]
    public async Task<IActionResult> CreateNewVersion(int documentId, IFormFile file)
    {
        return await ExecuteSafeAsync(async () =>
        {
            var version = await _minioService.CreateNewVersionAsync(documentId, file);
            return Ok(version);
        });
    }

    /// <summary>
    /// Удалить конкретную версию документа
    /// </summary>
    [HttpDelete("documents/versions/{versionId}")]
    public async Task<IActionResult> DeleteDocumentVersion(int versionId)
    {
        return await ExecuteSafeAsync(async () =>
        {
            await _minioService.DeleteDocumentVersionAsync(versionId);
            return Ok($"Version {versionId} deleted");
        });
    }

    /// <summary>
    /// Скачать конкретную версию файла
    /// </summary>
    [HttpGet("documents/versions/{versionId}/download")]
    public async Task<IActionResult> DownloadVersion(int versionId)
    {
        return await ExecuteSafeAsync(async () =>
        {
            var version = await _minioService.GetDocumentVersionByIdAsync(versionId);
            if (version == null)
                return NotFound($"Version with ID {versionId} not found");

            // Извлекаем bucketName из MinioObjectName (первый сегмент пути)
            var parts = version.MinioObjectName.Split('/');
            var bucketName = parts[0];

            var stream = await _minioService.GetFileAsync(bucketName, version.MinioObjectName);
            return File(stream, "application/octet-stream", version.OriginalFileName);
        });
    }

    #endregion

    #region Низкоуровневые операции с файлами

    [HttpPost("get-file")]
    public async Task<IActionResult> GetFile(string bucketName, string objectName)
    {
        return await ExecuteSafeAsync(async () =>
        {
            var stream = await _minioService.GetFileAsync(bucketName, objectName);
            return File(stream, "application/octet-stream", objectName);
        });
    }

    [HttpPost("get-file-url")]
    public async Task<IActionResult> GetFileUrl(string bucketName, string objectName)
    {
        return await ExecuteSafeAsync(async () =>
        {
            var url = await _minioService.GetFileUrlAsync(bucketName, objectName);
            return Ok(url);
        });
    }

    #endregion

    #region Парсинг содержимого файлов

    [HttpPost("get-excel-content")]
    public async Task<IActionResult> GetExcelFileContentAsJson(string bucketName, string objectName)
    {
        return await ExecuteSafeAsync(async () =>
        {
            var content = await _minioService.GetExcelFileContentAsJson(bucketName, objectName);
            return Ok(content);
        });
    }

    [HttpPost("get-excel-content-limits")]
    public async Task<IActionResult> GetExcelFileContentAsJsonWithLimits(string bucketName, string objectName, double inputX1, double inputX2, double inputY1, double inputY2)
    {
        return await ExecuteSafeAsync(async () =>
        {
            var content = await _minioService.GetExcelFileContentAsJsonWithLimits(bucketName, objectName, inputX1, inputX2, inputY1, inputY2);
            return Ok(content);
        });
    }

    [HttpPost("get-spc-content")]
    public async Task<IActionResult> GetSPCFileContentAsJson(string bucketName, string objectName)
    {
        return await ExecuteSafeAsync(async () =>
        {
            var content = await _minioService.GetSPCFileContentAsJson(bucketName, objectName);
            return Ok(content);
        });
    }

    #endregion
}