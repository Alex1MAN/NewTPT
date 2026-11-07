using System.Globalization;
using JWTAuthTemplate.Application.Exceptions;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>
/// Маршруты master-версии (/User) для старого клиента: пользователь задаётся параметрами.
/// Логика общая с /roles и /sessions.
/// </summary>
[ApiController]
[Route("User")]
public sealed class UserController : BaseController
{
    private const string LegacyTimeFormat = "dd.MM.yyyy HH:mm:ss";
    private readonly IUserService _users;
    private readonly IRoleService _roles;
    private readonly ISessionService _sessions;

    public UserController(IUserService users, IRoleService roles, ISessionService sessions)
        => (_users, _roles, _sessions) = (users, roles, sessions);

    [HttpGet("Get")]
    public Task<IActionResult> Get(string username) => GetByUsername(username, "User not found.");

    [HttpGet("/users/get-by-username")]
    public Task<IActionResult> GetByUsername(string username)
        => GetByUsername(username, $"User with username '{username}' was not found");

    private Task<IActionResult> GetByUsername(string username, string notFoundMessage) => ExecuteSafeAsync(async () =>
        Ok(await _users.GetByUsername(username) ?? throw new KeyNotFoundException(notFoundMessage)));

    /// <summary>Повторное назначение роли — 400, как в master-версии (маршрут /roles возвращает 409).</summary>
    [HttpPost("AddRole")]
    public Task<IActionResult> AddRole(string username, [FromBody] RoleDTO role) => ExecuteSafeAsync(async () =>
    {
        var userId = await _users.GetIdByUsernameAsync(username);
        try { await _roles.AssignRoleAsync(userId, role.Name); }
        catch (ConflictException) { throw new ArgumentException($"User already in role '{role.Name}'."); }
        return Ok(true);
    });

    /// <summary>Снятие отсутствующей роли — 404, как в master-версии (маршрут /roles возвращает 409).</summary>
    [HttpDelete("RemoveRole")]
    public Task<IActionResult> RemoveRole(string username, [FromBody] RoleDTO role) => ExecuteSafeAsync(async () =>
    {
        var userId = await _users.GetIdByUsernameAsync(username);
        try { await _roles.RemoveRoleAsync(userId, role.Name); }
        catch (ConflictException) { throw new KeyNotFoundException("User is not in role."); }
        return Ok(true);
    });

    [HttpPost("SaveStatus")]
    public Task<IActionResult> SaveStatus(string userId, [FromBody] Dictionary<string, object> statusParams) => ExecuteSafeAsync(async () =>
    {
        await _sessions.SaveStatusAsync(userId, statusParams);
        return Ok(true);
    });

    [HttpGet("GetLatestStatus")]
    public Task<IActionResult> GetLatestStatus(string userId) => ExecuteSafeAsync(async () =>
        Ok(await _sessions.GetLatestAsync(userId) ?? throw StatusNotFound()));

    [HttpPost("GetAllStatusesByFileName")]
    public Task<IActionResult> GetAllStatusesByFileName(string userId, string fileName, string fileExtension)
        => ExecuteSafeAsync(async () => Ok(await _sessions.GetAllByFileAsync(userId, fileName, fileExtension)));

    [HttpPost("GetLatestStatusByFileName")]
    public Task<IActionResult> GetLatestStatusByFileName(string userId, string fileName, string fileExtension)
        => ExecuteSafeAsync(async () => Ok(await _sessions.GetLatestByFileAsync(userId, fileName, fileExtension) ?? throw StatusNotFound()));

    /// <summary>Первый статус не раньше timeFilter (формат dd.MM.yyyy HH:mm:ss, UTC) — семантика master-версии.</summary>
    [HttpPost("GetLatestStatusByFileNameAndTime")]
    public Task<IActionResult> GetLatestStatusByFileNameAndTime(string userId, string fileName, string fileExtension, string timeFilter)
        => ExecuteSafeAsync(async () =>
        {
            if (!DateTime.TryParseExact(timeFilter, LegacyTimeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var sinceUtc))
                throw new ArgumentException("Wrong date format: use dd.MM.yyyy HH:mm:ss (UTC).");
            return Ok(await _sessions.GetFirstByFileSinceAsync(userId, fileName, fileExtension, sinceUtc) ?? throw StatusNotFound());
        });

    private static KeyNotFoundException StatusNotFound() => new("Status not found.");
}
