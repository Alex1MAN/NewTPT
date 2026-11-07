using System.Globalization;
using System.Security.Claims;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Статусы сессий текущего пользователя (пользователь определяется по JWT).</summary>
[ApiController]
[Authorize]
[Route(ApiRoutes.Prefix + "/sessions/statuses")]
public sealed class SessionsController : BaseController
{
    private readonly ISessionService _sessionService;

    public SessionsController(ISessionService sessionService) => _sessionService = sessionService;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new ArgumentException("User ID not found in token.");

    /// <summary>Сохранение статуса сессии.</summary>
    /// <remarks>
    /// Тело — произвольный JSON-объект параметров. Ключи fileName и fileExtension (регистр не важен)
    /// дополнительно сохраняются в отдельные столбцы и используются для фильтрации.
    /// </remarks>
    /// <response code="201">Статус сохранён: { "id": ... }.</response>
    /// <response code="400">Тело пустое.</response>
    /// <response code="401">Нет действующего токена.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> SaveStatus([FromBody] Dictionary<string, object>? statusParams) => ExecuteSafeAsync(async () =>
    {
        if (statusParams == null)
            return Error(StatusCodes.Status400BadRequest, "Request body cannot be empty");
        var id = await _sessionService.SaveStatusAsync(CurrentUserId, statusParams);
        return Created($"/{ApiRoutes.Prefix}/sessions/statuses/latest", new { id });
    });

    /// <summary>Все статусы по файлу, от новых к старым.</summary>
    /// <param name="fileName">Имя файла (без расширения), как оно сохранено в статусе.</param>
    /// <param name="fileExtension">Расширение файла без точки.</param>
    /// <response code="200">Список статусов (пустой, если ничего не найдено).</response>
    /// <response code="400">Не указан один из параметров.</response>
    /// <response code="401">Нет действующего токена.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<UserSessionStatusDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> GetStatuses([FromQuery] string fileName, [FromQuery] string fileExtension)
        => ExecuteSafeAsync(async () => Ok(await _sessionService.GetByFileAsync(CurrentUserId, fileName, fileExtension)));

    /// <summary>Последний статус пользователя.</summary>
    /// <remarks>
    /// Без параметров — последний статус по всем файлам. С fileName и fileExtension — последний статус файла.
    /// Параметр asOf (ISO 8601, без зоны трактуется как UTC) ограничивает выборку статусами не позже указанного момента.
    /// </remarks>
    /// <param name="fileName">Имя файла (необязательно, указывается вместе с fileExtension).</param>
    /// <param name="fileExtension">Расширение файла без точки.</param>
    /// <param name="asOf">Момент времени, например 2026-04-03T17:05:00Z.</param>
    /// <response code="200">Последний статус.</response>
    /// <response code="400">Неполный набор параметров или некорректная дата.</response>
    /// <response code="401">Нет действующего токена.</response>
    /// <response code="404">Статусов нет.</response>
    [HttpGet("latest")]
    [ProducesResponseType(typeof(UserSessionStatusDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetLatestStatus([FromQuery] string? fileName, [FromQuery] string? fileExtension, [FromQuery] string? asOf)
        => ExecuteSafeAsync(async () =>
        {
            var asOfUtc = asOf is null ? (DateTime?)null : ParseUtc(asOf, "asOf");
            var status = await _sessionService.GetLatestAsync(CurrentUserId, fileName, fileExtension, asOfUtc);
            return status is not null ? Ok(status) : Error(StatusCodes.Status404NotFound, "No session status found.");
        });

    /// <summary>Первый статус файла, зафиксированный не раньше указанного момента.</summary>
    /// <remarks>Дополняет /latest: возвращает самый ранний статус с ActualAt ≥ since (ISO 8601, без зоны трактуется как UTC).</remarks>
    /// <param name="fileName">Имя файла (без расширения).</param>
    /// <param name="fileExtension">Расширение файла без точки.</param>
    /// <param name="since">Момент времени, например 2026-04-03T17:05:00Z.</param>
    /// <response code="200">Первый подходящий статус.</response>
    /// <response code="400">Не указан параметр или некорректная дата.</response>
    /// <response code="401">Нет действующего токена.</response>
    /// <response code="404">Статусов после указанного момента нет.</response>
    [HttpGet("first")]
    [ProducesResponseType(typeof(UserSessionStatusDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetFirstStatusSince([FromQuery] string fileName, [FromQuery] string fileExtension, [FromQuery] string since)
        => ExecuteSafeAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(since))
                throw new ArgumentException("Parameter 'since' is required.");
            var status = await _sessionService.GetFirstSinceAsync(CurrentUserId, fileName, fileExtension, ParseUtc(since, "since"));
            return status is not null ? Ok(status) : Error(StatusCodes.Status404NotFound, "No session status found.");
        });

    /// <summary>Разбор даты ISO 8601; значение без зоны считается UTC.</summary>
    private static DateTime ParseUtc(string value, string parameter)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : throw new ArgumentException($"Parameter '{parameter}' must be a valid ISO 8601 datetime string (e.g., 2026-04-03T17:05:00Z or 2026-04-03T17:05:00)");
}
