using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Application.Services;
using JWTAuthTemplate.Shared.Dtos;
using JWTAuthTemplate.WebAPI.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>
/// Файлы, документы и их версии в MinIO. Маршруты master-версии (CamelCase) сохранены
/// для старого клиента и обслуживаются теми же сервисами, что и новые.
/// </summary>
[ApiController]
[Route("minio")]
public sealed class MinioController : BaseController
{
    private const long ChunkRequestLimit = ChunkUploadService.MaxChunkBytes + 65536;
    private readonly IDocumentService _documents;
    private readonly IFileContentService _content;
    private readonly IChunkUploadService _chunks;

    public MinioController(IDocumentService documents, IFileContentService content, IChunkUploadService chunks)
        => (_documents, _content, _chunks) = (documents, content, chunks);

    #region Бакеты и загрузка

    [HttpPost("create-bucket")]
    [HttpPost("CreateBucket")]
    public Task<IActionResult> CreateBucket(string bucketName) => ExecuteSafeAsync(async () =>
    {
        await _documents.CreateBucketAsync(bucketName);
        return Ok(true);
    });

    /// <summary>Загрузить файлы; для каждого создаётся документ с первой версией.</summary>
    [HttpPost("upload-files")]
    public Task<IActionResult> UploadFiles([FromQuery] string bucketName, [FromForm] List<IFormFile> filesData)
        => Upload(bucketName, filesData, checkUnique: false);

    /// <summary>Маршрут master-версии: бакет передаётся полем формы.</summary>
    [HttpPost("UploadFilesUpdateReferences")]
    public Task<IActionResult> UploadFilesUpdateReferences([FromForm] string bucketName, [FromForm] List<IFormFile> filesData)
        => Upload(bucketName, filesData, checkUnique: false);

    /// <summary>То же, но с предварительной проверкой уникальности имён файлов (409 при повторе).</summary>
    [HttpPost("UploadFilesUpdateReferences_2")]
    public Task<IActionResult> UploadFilesUnique([FromForm] string bucketName, [FromForm] List<IFormFile> filesData)
        => Upload(bucketName, filesData, checkUnique: true);

    private Task<IActionResult> Upload(string bucketName, List<IFormFile> filesData, bool checkUnique) => ExecuteSafeAsync(async () =>
    {
        if (checkUnique)
            await _documents.EnsureUniqueFilesAsync(bucketName, filesData.Select(f => f.FileName).ToList());
        return Ok(await _documents.UploadFilesAsync(bucketName, filesData.ToFileUploads()));
    });

    /// <summary>Положить файл в MinIO без создания документа.</summary>
    [HttpPost("upload-file")]
    public Task<IActionResult> UploadFile(string bucketName, string objectName, IFormFile file) => ExecuteSafeAsync(async () =>
    {
        await _documents.PutObjectAsync(bucketName, objectName, file.ToFileUpload());
        return Ok(true);
    });

    /// <summary>Тестовая загрузка master-версии: бакет "44444", объект "test13".</summary>
    [HttpPost("TestUploadFile")]
    public Task<IActionResult> TestUploadFile(IFormFile file) => ExecuteSafeAsync(async () =>
    {
        if (file.Length == 0) throw new ArgumentException("File is required.");
        await _documents.CreateBucketAsync("44444");
        await _documents.PutObjectAsync("44444", "test13", file.ToFileUpload());
        return Ok(true);
    });

    [HttpPost("UploadChunk")]
    [RequestSizeLimit(ChunkRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ChunkRequestLimit, MemoryBufferThreshold = (int)ChunkRequestLimit)]
    public Task<IActionResult> UploadChunk([FromForm] string bucketName, [FromForm] string uploadId,
        [FromForm] string fileName, [FromForm] int chunkIndex, [FromForm] int totalChunks,
        IFormFile? chunk, CancellationToken ct) => ExecuteSafeAsync(async () =>
    {
        if (chunk == null) throw new ArgumentException("Chunk missing.");
        using var stream = chunk.OpenReadStream();
        await _chunks.UploadChunk(new UploadChunkRequest
        {
            BucketName = bucketName,
            UploadId = uploadId,
            FileName = fileName,
            ChunkIndex = chunkIndex,
            TotalChunks = totalChunks
        }, stream, chunk.Length, ct);
        return Ok(new { success = true, chunkIndex });
    });

    [HttpPost("CompleteUpload")]
    public Task<IActionResult> CompleteUpload([FromBody] CompleteUploadRequest request, CancellationToken ct)
        => ExecuteSafeAsync(async () => Ok(await _chunks.CompleteUpload(request, ct)));

    #endregion

    #region Документы и версии

    [HttpGet("documents/{userId}")]
    public Task<IActionResult> GetDocuments(string userId)
        => ExecuteSafeAsync(async () => Ok(await _documents.GetDocumentsAsync(userId)));

    [HttpGet("documents/detail/{documentId}")]
    public Task<IActionResult> GetDocument(int documentId)
        => ExecuteSafeAsync(async () => Ok(await _documents.GetDocumentAsync(documentId)));

    /// <summary>Удалить документ со всеми версиями.</summary>
    [HttpDelete("documents/{documentId}")]
    public Task<IActionResult> DeleteDocument(int documentId) => ExecuteSafeAsync(async () =>
    {
        await _documents.DeleteDocumentAsync(documentId);
        return Ok(true);
    });

    [HttpGet("documents/{documentId}/versions")]
    public Task<IActionResult> GetDocumentVersions(int documentId)
        => ExecuteSafeAsync(async () => Ok(await _documents.GetVersionsAsync(documentId)));

    [HttpPost("documents/{documentId}/versions")]
    public Task<IActionResult> CreateNewVersion(int documentId, IFormFile file)
        => ExecuteSafeAsync(async () => Ok(await _documents.CreateVersionAsync(documentId, file.ToFileUpload())));

    [HttpGet("documents/versions/{versionId}")]
    public Task<IActionResult> GetDocumentVersion(int versionId)
        => ExecuteSafeAsync(async () => Ok(await _documents.GetVersionAsync(versionId)));

    /// <summary>Удалить версию; последнюю версию удалить нельзя (409).</summary>
    [HttpDelete("documents/versions/{versionId}")]
    public Task<IActionResult> DeleteDocumentVersion(int versionId) => ExecuteSafeAsync(async () =>
    {
        await _documents.DeleteVersionAsync(versionId);
        return Ok(true);
    });

    [HttpGet("documents/versions/{versionId}/download")]
    public Task<IActionResult> DownloadVersion(int versionId) => ExecuteSafeAsync(async () =>
    {
        var (content, fileName) = await _documents.OpenVersionAsync(versionId);
        return File(content, "application/octet-stream", fileName);
    });

    [HttpPost("AddReferenceInPostgre")]
    public Task<IActionResult> AddReferenceInPostgre([FromBody] UserReferencesInMinio reference) => ExecuteSafeAsync(async () =>
    {
        await _documents.AddReferenceAsync(reference);
        return CreatedAtAction(nameof(GetFileReferencesByUserId), new { userId = reference.UserId }, reference);
    });

    [HttpGet("GetFileReferencesByUserId")]
    public Task<IActionResult> GetFileReferencesByUserId(string userId)
        => ExecuteSafeAsync(async () => Ok(await _documents.GetReferencesAsync(userId)));

    #endregion

    #region Файлы

    [HttpPost("get-file")]
    public Task<IActionResult> GetFile(string bucketName, string objectName) => DownloadObject(bucketName, objectName);

    [HttpGet("GetFileContent")]
    public Task<IActionResult> GetFileContent(string bucketName, string fileName) => DownloadObject(bucketName, fileName);

    [HttpPost("get-file-url")]
    public Task<IActionResult> GetFileUrl(string bucketName, string objectName)
        => ExecuteSafeAsync(async () => Ok(await _documents.GetObjectUrlAsync(bucketName, objectName)));

    [HttpGet("GetUrlFileFromMinio")]
    public Task<IActionResult> GetUrlFileFromMinio(string bucketName, string objectName)
        => ExecuteSafeAsync(async () => Ok(new { Url = await _documents.GetObjectUrlAsync(bucketName, objectName) }));

    private Task<IActionResult> DownloadObject(string bucketName, string objectName) => ExecuteSafeAsync(async () =>
        File(await _documents.OpenObjectAsync(bucketName, objectName), "application/octet-stream", Path.GetFileName(objectName)));

    #endregion

    #region Содержимое Excel/SPC

    [HttpPost("get-excel-content")]
    public Task<IActionResult> GetExcelContent(string bucketName, string objectName)
        => ExecuteSafeAsync(async () => Ok(await _content.GetExcelContentAsync(bucketName, objectName)));

    [HttpGet("GetAllTableFromExcel")]
    public Task<IActionResult> GetAllTableFromExcel(string bucketName, string fileName) => GetExcelContent(bucketName, fileName);

    [HttpPost("get-excel-content-limits")]
    public Task<IActionResult> GetExcelContentWithLimits(string bucketName, string objectName,
        double inputX1, double inputX2, double inputY1, double inputY2)
        => ExecuteSafeAsync(async () => Ok(await _content.GetExcelContentWithLimitsAsync(
            bucketName, objectName, inputX1, inputX2, inputY1, inputY2)));

    [HttpGet("GetTableFromExcelWithLimits")]
    public Task<IActionResult> GetTableFromExcelWithLimits(string bucketName, string fileName,
        double inputX1, double inputX2, double inputY1, double inputY2)
        => GetExcelContentWithLimits(bucketName, fileName, inputX1, inputX2, inputY1, inputY2);

    [HttpPost("get-spc-content")]
    public Task<IActionResult> GetSpcContent(string bucketName, string objectName)
        => ExecuteSafeAsync(async () => Ok(await _content.GetSpcContentAsync(bucketName, objectName)));

    [HttpGet("GetTablesFromSeveralSPC")]
    public Task<IActionResult> GetTablesFromSeveralSpc(string bucketName, [FromQuery] string[] fileNames)
        => ExecuteSafeAsync(async () => Ok(await _content.GetTablesFromSeveralSpcAsync(bucketName, fileNames)));

    [HttpGet("GetAllContentFromFiles")]
    public Task<IActionResult> GetAllContentFromFiles(string bucketName, [FromQuery] string[] fileNames,
        string allFilesExtension, double inputX1, double inputX2, double inputY1, double inputY2)
        => ExecuteSafeAsync(async () => Ok(await _content.GetAllContentFromFilesAsync(
            bucketName, fileNames, allFilesExtension, inputX1, inputX2, inputY1, inputY2)));

    #endregion
}
