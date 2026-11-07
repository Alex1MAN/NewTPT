using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Прямой доступ к объектам хранилища (MinIO) и чтение табличных файлов.</summary>
[ApiController]
[Route(ApiRoutes.Prefix + "/storage")]
public sealed class StorageController : BaseController
{
    private readonly IObjectStorage _storage;
    private readonly ITableContentService _tables;

    public StorageController(IObjectStorage storage, ITableContentService tables) => (_storage, _tables) = (storage, tables);

    /// <summary>Создание бакета (если он ещё не существует).</summary>
    /// <remarks>Бакет пользователя создаётся автоматически при регистрации; метод нужен для восстановления хранилища.</remarks>
    /// <param name="bucket">Имя бакета (обычно идентификатор пользователя).</param>
    /// <response code="204">Бакет существует.</response>
    /// <response code="400">Недопустимое имя бакета.</response>
    [HttpPut("buckets/{bucket}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> EnsureBucket(string bucket) => ExecuteSafeAsync(async () =>
    {
        await _storage.EnsureBucketAsync(bucket);
        return NoContent();
    });

    /// <summary>Скачивание объекта из бакета.</summary>
    /// <param name="bucket">Имя бакета.</param>
    /// <param name="objectName">Полное имя объекта, например {userId}/{documentId}/v1_report.xlsx.</param>
    /// <response code="200">Содержимое объекта (application/octet-stream).</response>
    /// <response code="404">Объект или бакет не найдены.</response>
    [HttpGet("{bucket}/objects/{*objectName}")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DownloadObject(string bucket, string objectName) => ExecuteSafeAsync(async () =>
    {
        var bytes = await _storage.ReadAllBytesAsync(bucket, objectName);
        return File(bytes, "application/octet-stream", Path.GetFileName(objectName));
    });

    /// <summary>Временная ссылка (1 час) на скачивание объекта напрямую из хранилища.</summary>
    /// <param name="bucket">Имя бакета.</param>
    /// <param name="objectName">Полное имя объекта.</param>
    /// <response code="200">{ "url": "..." }.</response>
    /// <response code="404">Объект или бакет не найдены.</response>
    [HttpGet("{bucket}/presigned-urls/{*objectName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetPresignedUrl(string bucket, string objectName)
        => ExecuteSafeAsync(async () => Ok(new { url = await _storage.GetPresignedUrlAsync(bucket, objectName) }));

    /// <summary>Чтение таблиц из файлов Excel (xls, xlsx) и SPC.</summary>
    /// <remarks>
    /// Формат каждого файла определяется по расширению имени объекта; файлы разных форматов можно запрашивать вместе.
    /// Параметры x1, x2, y1, y2 задаются все вместе и применяются только к Excel: остаются столбцы с числовым заголовком
    /// из диапазона [x1; x2], числовые значения ограничиваются отрезком [y1; y2]. Поле <c>table</c> каждого элемента —
    /// JSON-текст вида {"labels": [...], "values": [[{...}, ...]]}.
    /// </remarks>
    /// <param name="bucket">Имя бакета.</param>
    /// <param name="objectNames">Имена объектов (параметр повторяется; от 1 до 1000).</param>
    /// <param name="x1">Первая граница диапазона заголовков столбцов (только Excel).</param>
    /// <param name="x2">Вторая граница диапазона заголовков столбцов (только Excel).</param>
    /// <param name="y1">Первая граница диапазона значений (только Excel).</param>
    /// <param name="y2">Вторая граница диапазона значений (только Excel).</param>
    /// <response code="200">Таблицы в порядке перечисления объектов.</response>
    /// <response code="400">Неподдерживаемое расширение, неполный диапазон или пустой список.</response>
    /// <response code="404">Объект или бакет не найдены.</response>
    [HttpGet("{bucket}/tables")]
    [ProducesResponseType(typeof(List<TableContentDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> ReadTables(string bucket, [FromQuery] string[] objectNames,
        [FromQuery] double? x1, [FromQuery] double? x2, [FromQuery] double? y1, [FromQuery] double? y2) => ExecuteSafeAsync(async () =>
    {
        var bounds = new[] { x1, x2, y1, y2 };
        if (bounds.Any(b => b.HasValue) && !bounds.All(b => b.HasValue))
            return Error(StatusCodes.Status400BadRequest, "Parameters x1, x2, y1 and y2 must be specified together.");
        var range = x1.HasValue ? new TableRange(x1!.Value, x2!.Value, y1!.Value, y2!.Value) : null;
        return Ok(await _tables.ReadTablesAsync(bucket, objectNames, range));
    });
}
