using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Предобработка спектральных данных.</summary>
[ApiController]
[Route(ApiRoutes.Prefix + "/preprocessing")]
public sealed class PreprocessingController : BaseController
{
    private readonly IPreprocessService _preprocess;

    public PreprocessingController(IPreprocessService preprocess) => _preprocess = preprocess;

    /// <summary>Применение шагов предобработки к рядам X и Y.</summary>
    /// <remarks>Поддерживаемые шаги в <c>params.prepX</c> / <c>params.prepY</c>: "scaling" (центрирование и масштабирование) и "normalization" (нормировка на единичную длину); шаги применяются по порядку.</remarks>
    /// <response code="200">Преобразованные ряды.</response>
    /// <response code="400">Пустые или нечисловые ряды, неизвестный шаг.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PreprocessResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Process([FromBody] PreprocessRequest request)
        => ExecuteSafeAsync(() => Ok(_preprocess.ProcessData(request)));
}
