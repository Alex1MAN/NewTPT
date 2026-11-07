using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Services;
using JWTAuthTemplate.Shared.Dtos;
using JWTAuthTemplate.WebAPI.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Документы пользователя, их версии и загрузка больших файлов по частям.</summary>
[ApiController]
[Route(ApiRoutes.Prefix)]
public sealed class DocumentsController : BaseController
{
    private const long ChunkRequestLimit = ChunkUploadService.MaxChunkBytes + 65536;
    private readonly IDocumentService _documents;
    private readonly IChunkUploadService _chunks;

    public DocumentsController(IDocumentService documents, IChunkUploadService chunks)
        => (_documents, _chunks) = (documents, chunks);

    #region Документы пользователя

    /// <summary>Список документов пользователя.</summary>
    /// <param name="userId">Идентификатор пользователя (он же имя бакета).</param>
    /// <response code="200">Документы с данными текущей версии, от недавно обновлённых к старым.</response>
    [HttpGet("users/{userId}/documents")]
    [ProducesResponseType(typeof(List<FileReferenceDTO>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetUserDocuments(string userId)
        => ExecuteSafeAsync(async () => Ok(await _documents.GetDocumentsAsync(userId)));

    /// <summary>Загрузка файлов: для каждого файла создаётся документ с первой версией.</summary>
    /// <remarks>Форма multipart/form-data с одним или несколькими полями <c>files</c>. Объект в хранилище получает путь {userId}/{documentId}/v1_{имя}.</remarks>
    /// <param name="userId">Идентификатор пользователя (он же имя бакета).</param>
    /// <param name="files">Загружаемые файлы.</param>
    /// <param name="allowDuplicateNames">Разрешить имя файла, которое уже есть у пользователя (по умолчанию — конфликт 409).</param>
    /// <response code="200">Созданные документы.</response>
    /// <response code="400">Файлы не переданы или пусты.</response>
    /// <response code="404">Пользователь не найден.</response>
    /// <response code="409">Файл с таким именем уже существует.</response>
    [HttpPost("users/{userId}/documents")]
    [ProducesResponseType(typeof(UploadResultDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> UploadDocuments(string userId, [FromForm] List<IFormFile> files, [FromQuery] bool allowDuplicateNames = false)
        => ExecuteSafeAsync(async () => Ok(await _documents.UploadFilesAsync(userId, files.ToFileUploads(), allowDuplicateNames)));

    #endregion

    #region Документы и версии

    /// <summary>Документ по идентификатору.</summary>
    /// <response code="200">Документ с данными текущей версии.</response>
    /// <response code="404">Документ не найден.</response>
    [HttpGet("documents/{documentId:int}")]
    [ProducesResponseType(typeof(FileReferenceDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetDocument(int documentId)
        => ExecuteSafeAsync(async () => Ok(await _documents.GetDocumentAsync(documentId)));

    /// <summary>Удаление документа со всеми версиями и файлами в хранилище.</summary>
    /// <response code="204">Документ удалён.</response>
    /// <response code="404">Документ не найден.</response>
    [HttpDelete("documents/{documentId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DeleteDocument(int documentId) => ExecuteSafeAsync(async () =>
    {
        await _documents.DeleteDocumentAsync(documentId);
        return NoContent();
    });

    /// <summary>Версии документа, от новых к старым.</summary>
    /// <response code="200">Список версий.</response>
    [HttpGet("documents/{documentId:int}/versions")]
    [ProducesResponseType(typeof(List<DocumentVersionDTO>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetVersions(int documentId)
        => ExecuteSafeAsync(async () => Ok(await _documents.GetVersionsAsync(documentId)));

    /// <summary>Загрузка новой версии документа.</summary>
    /// <remarks>Форма multipart/form-data с полем <c>file</c>; расширение должно совпадать с расширением документа. Новая версия становится текущей.</remarks>
    /// <response code="200">Созданная версия.</response>
    /// <response code="400">Файл пуст или расширение не совпадает.</response>
    /// <response code="404">Документ не найден.</response>
    [HttpPost("documents/{documentId:int}/versions")]
    [ProducesResponseType(typeof(DocumentVersionDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> CreateVersion(int documentId, IFormFile file)
        => ExecuteSafeAsync(async () => Ok(await _documents.CreateVersionAsync(documentId, file.ToFileUpload())));

    /// <summary>Версия документа по идентификатору.</summary>
    /// <response code="200">Данные версии.</response>
    /// <response code="404">Версия не найдена.</response>
    [HttpGet("document-versions/{versionId:int}")]
    [ProducesResponseType(typeof(DocumentVersionDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetVersion(int versionId)
        => ExecuteSafeAsync(async () => Ok(await _documents.GetVersionAsync(versionId)));

    /// <summary>Удаление версии документа.</summary>
    /// <remarks>Если удаляется текущая версия, текущей становится предыдущая. Единственную версию удалить нельзя — удаляйте документ.</remarks>
    /// <response code="204">Версия удалена.</response>
    /// <response code="404">Версия не найдена.</response>
    /// <response code="409">Это единственная версия документа.</response>
    [HttpDelete("document-versions/{versionId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> DeleteVersion(int versionId) => ExecuteSafeAsync(async () =>
    {
        await _documents.DeleteVersionAsync(versionId);
        return NoContent();
    });

    /// <summary>Скачивание содержимого версии.</summary>
    /// <response code="200">Файл (application/octet-stream) с оригинальным именем.</response>
    /// <response code="404">Версия не найдена.</response>
    [HttpGet("document-versions/{versionId:int}/content")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DownloadVersion(int versionId) => ExecuteSafeAsync(async () =>
    {
        var (content, fileName) = await _documents.OpenVersionAsync(versionId);
        return File(content, "application/octet-stream", fileName);
    });

    #endregion

    #region Загрузка по частям

    /// <summary>Загрузка одной части файла.</summary>
    /// <remarks>
    /// Форма multipart/form-data: поля <c>fileName</c>, <c>totalChunks</c> и файл <c>chunk</c> (до 8 МиБ).
    /// Части можно слать параллельно и в любом порядке; повтор той же части идемпотентен, другая часть с тем же индексом — конфликт.
    /// </remarks>
    /// <param name="userId">Идентификатор пользователя (он же имя бакета).</param>
    /// <param name="uploadId">Произвольный идентификатор сеанса загрузки, выбранный клиентом.</param>
    /// <param name="chunkIndex">Индекс части, начиная с 0.</param>
    /// <param name="fileName">Имя итогового файла (одинаковое для всех частей).</param>
    /// <param name="totalChunks">Общее число частей (одинаковое для всех частей).</param>
    /// <param name="chunk">Содержимое части.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <response code="200">Часть принята: { "chunkIndex": N }.</response>
    /// <response code="400">Нет файла части, неверные индексы или имя файла.</response>
    /// <response code="404">Пользователь не найден.</response>
    /// <response code="409">Часть с таким индексом уже загружена с другим содержимым.</response>
    /// <response code="413">Часть больше допустимого размера.</response>
    [HttpPut("users/{userId}/uploads/{uploadId}/chunks/{chunkIndex:int}")]
    [RequestSizeLimit(ChunkRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ChunkRequestLimit, MemoryBufferThreshold = (int)ChunkRequestLimit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public Task<IActionResult> UploadChunk(string userId, string uploadId, int chunkIndex,
        [FromForm] string fileName, [FromForm] int totalChunks, IFormFile? chunk, CancellationToken ct) => ExecuteSafeAsync(async () =>
    {
        if (chunk == null) throw new ArgumentException("Chunk missing.");
        using var stream = chunk.OpenReadStream();
        await _chunks.UploadChunk(new UploadChunkRequest
        {
            BucketName = userId,
            UploadId = uploadId,
            FileName = fileName,
            ChunkIndex = chunkIndex,
            TotalChunks = totalChunks
        }, stream, chunk.Length, ct);
        return Ok(new { chunkIndex });
    });

    /// <summary>Завершение загрузки по частям: сборка файла и создание документа.</summary>
    /// <remarks>Повторный вызов для уже собранного файла возвращает ту же квитанцию и не создаёт второй документ.</remarks>
    /// <param name="userId">Идентификатор пользователя (он же имя бакета).</param>
    /// <param name="uploadId">Идентификатор сеанса загрузки.</param>
    /// <param name="request">Имя файла и общее число частей.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <response code="200">Квитанция с идентификатором документа и именем объекта.</response>
    /// <response code="400">Имя файла или число частей не совпадают с сеансом.</response>
    /// <response code="404">Сеанс загрузки не найден.</response>
    /// <response code="409">Загружены не все части.</response>
    [HttpPost("users/{userId}/uploads/{uploadId}/complete")]
    [ProducesResponseType(typeof(CompleteUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> CompleteUpload(string userId, string uploadId, [FromBody] UploadCompletionDTO request, CancellationToken ct)
        => ExecuteSafeAsync(async () => Ok(await _chunks.CompleteUpload(new CompleteUploadRequest
        {
            BucketName = userId,
            UploadId = uploadId,
            FileName = request.FileName,
            TotalChunks = request.TotalChunks
        }, ct)));

    #endregion
}
