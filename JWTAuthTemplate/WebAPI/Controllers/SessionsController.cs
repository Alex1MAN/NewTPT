using System.Globalization;
using System.Security.Claims;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Статусы сессий текущего авторизованного пользователя.</summary>
[ApiController]
[Authorize]
[Route("sessions")]
public sealed class SessionsController : BaseController
{
    private readonly ISessionService _sessionService;

    public SessionsController(ISessionService sessionService) => _sessionService = sessionService;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new ArgumentException("User ID not found in token.");

    [HttpPost("save-status")]
    public Task<IActionResult> SaveStatus([FromBody] Dictionary<string, object>? statusParams) => ExecuteSafeAsync(async () =>
    {
        if (statusParams == null)
            return BadRequest("Request body cannot be empty");
        var id = await _sessionService.SaveStatusAsync(CurrentUserId, statusParams);
        return Ok(new { id, message = "Status saved successfully" });
    });

    [HttpGet("latest-status")]
    public Task<IActionResult> GetLatestStatus()
        => ExecuteSafeAsync(async () => Ok(Compact(await _sessionService.GetLatestAsync(CurrentUserId))));

    [HttpGet("all-by-file-name")]
    public Task<IActionResult> GetAllStatusesByFileName([FromQuery] string fileName, [FromQuery] string fileExtension)
        => ExecuteSafeAsync(async () =>
        {
            if (Missing(fileName, fileExtension) is { } error) return error;
            var statuses = await _sessionService.GetAllByFileAsync(CurrentUserId, fileName, fileExtension);
            return statuses.Count > 0
                ? Ok(statuses.Select(Compact))
                : NotFound($"No session statuses found for file '{fileName}' with extension '{fileExtension}'");
        });

    [HttpGet("latest-by-file-name")]
    public Task<IActionResult> GetLatestStatusByFileName([FromQuery] string fileName, [FromQuery] string fileExtension)
        => ExecuteSafeAsync(async () =>
        {
            if (Missing(fileName, fileExtension) is { } error) return error;
            var status = await _sessionService.GetLatestByFileAsync(CurrentUserId, fileName, fileExtension);
            return status is not null
                ? Ok(Compact(status))
                : NotFound($"No session status found for file '{fileName}' with extension '{fileExtension}'");
        });

    /// <summary>Последний статус не позже asOfTime (ISO 8601; без зоны — UTC).</summary>
    [HttpGet("latest-by-file-name-and-time")]
    public Task<IActionResult> GetLatestStatusByFileNameAndTime([FromQuery] string fileName, [FromQuery] string fileExtension,
        [FromQuery] string asOfTime) => ExecuteSafeAsync(async () =>
    {
        if (Missing(fileName, fileExtension) is { } error) return error;
        if (asOfTime is null) return BadRequest("Parameter 'asOfTime' is required");
        if (!DateTime.TryParse(asOfTime, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var asOfUtc))
            return BadRequest("Parameter 'asOfTime' must be a valid ISO 8601 datetime string (e.g., 2026-04-03T17:05:00Z or 2026-04-03T17:05:00)");

        var status = await _sessionService.GetLatestByFileAsOfAsync(CurrentUserId, fileName, fileExtension, asOfUtc);
        return status is not null
            ? Ok(Compact(status))
            : NotFound($"No session status found for file '{fileName}' with extension '{fileExtension}' as of {asOfTime}");
    });

    /// <summary>Новые маршруты не возвращают исходную JSON-строку параметров — только словарь.</summary>
    private static UserSessionStatusDTO? Compact(UserSessionStatusDTO? status)
    {
        if (status is not null) status.StatusParams = null;
        return status;
    }

    private IActionResult? Missing(string fileName, string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return BadRequest("Parameter 'fileName' is required");
        if (string.IsNullOrWhiteSpace(fileExtension)) return BadRequest("Parameter 'fileExtension' is required");
        return null;
    }
}
