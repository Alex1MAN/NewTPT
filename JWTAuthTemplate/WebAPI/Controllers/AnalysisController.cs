using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Application.Validation;
using JWTAuthTemplate.Shared.Dtos;
using JWTAuthTemplate.WebAPI.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Многомерный анализ данных (PCA и другие зарегистрированные методы).</summary>
[ApiController]
[Route(ApiRoutes.Prefix + "/analysis")]
public sealed class AnalysisController : BaseController
{
    private const int MaxJsonBytes = 32 * 1024 * 1024;
    private readonly IAnalysisService _analysis;

    public AnalysisController(IAnalysisService analysis) => _analysis = analysis;

    /// <summary>Доступные методы анализа.</summary>
    /// <response code="200">Имена методов, например ["PCA"].</response>
    [HttpGet("methods")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public IActionResult GetMethods() => Ok(_analysis.GetMethods());

    /// <summary>Анализ матрицы, переданной в теле запроса.</summary>
    /// <remarks>
    /// Поле <c>method</c> выбирает алгоритм (по умолчанию PCA), <c>data</c> — матрица «строки × признаки» (не менее двух строк),
    /// <c>columnNames</c> — имена признаков, <c>newData</c> — строки для проекции на найденные компоненты,
    /// <c>varianceThreshold</c> — доля объяснённой дисперсии для подсчёта нужного числа компонент.
    /// </remarks>
    /// <response code="200">Результат анализа (для PCA — нагрузки, счёты, объяснённая дисперсия).</response>
    /// <response code="400">Неизвестный метод или некорректная матрица.</response>
    [HttpPost]
    [RequestSizeLimit(MaxJsonBytes)]
    [ProducesResponseType(typeof(PcaResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Analyze([FromBody] AnalysisRequest request)
        => ExecuteSafeAsync(() => Ok(_analysis.Analyze(request)));

    /// <summary>Анализ таблицы из Excel-файла (.xlsx).</summary>
    /// <remarks>
    /// Источник указывается одним из двух способов: файл в поле формы <c>file</c> либо объект хранилища
    /// полями формы <c>bucket</c> и <c>objectName</c>. Первая строка листа — имена признаков, остальные — числовые данные.
    /// </remarks>
    /// <param name="file">Excel-файл (multipart/form-data).</param>
    /// <param name="bucket">Имя бакета, если файл уже загружен в хранилище.</param>
    /// <param name="objectName">Имя объекта в бакете.</param>
    /// <param name="method">Метод анализа (по умолчанию PCA).</param>
    /// <param name="varianceThreshold">Порог объяснённой дисперсии (по умолчанию 0.95).</param>
    /// <response code="200">Результат анализа.</response>
    /// <response code="400">Источник не указан или указан дважды, файл не .xlsx либо данные некорректны.</response>
    /// <response code="404">Объект или бакет не найдены.</response>
    [HttpPost("excel")]
    [RequestSizeLimit(AnalysisLimits.MaxExcelBytes)]
    [ProducesResponseType(typeof(PcaResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> AnalyzeExcel(IFormFile? file, [FromForm] string? bucket, [FromForm] string? objectName,
        [FromQuery] string method = "PCA", [FromQuery] double varianceThreshold = 0.95) => ExecuteSafeAsync(async () =>
    {
        bool hasFile = file != null, hasObject = !string.IsNullOrWhiteSpace(bucket) || !string.IsNullOrWhiteSpace(objectName);
        if (hasFile == hasObject)
            return Error(StatusCodes.Status400BadRequest, "Specify exactly one source: form file 'file' or form fields 'bucket' and 'objectName'.");
        if (hasObject && (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(objectName)))
            return Error(StatusCodes.Status400BadRequest, "Both 'bucket' and 'objectName' are required.");
        ExcelSource source = hasFile ? new UploadedExcel(file!.ToFileUpload()) : new StoredExcel(bucket!, objectName!);
        return Ok(await _analysis.AnalyzeExcelAsync(source, method, varianceThreshold));
    });
}
