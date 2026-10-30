using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.DTO.Identity;
using Microsoft.AspNetCore.Mvc;
namespace JWTAuthTemplate.WebAPI.Controllers;
[ApiController]
[Route("User")]
public sealed class UserController : BaseController
{
    private readonly ILegacyUserService _service;
    private readonly IUserService _users;
    public UserController(ILegacyUserService service, IUserService users) => (_service, _users) = (service, users);
    [HttpGet("Get")]
    public Task<IActionResult> Get(string username) => ExecuteSafeAsync(async () =>
        Ok(await _users.GetByUsername(username) ?? throw new KeyNotFoundException("User not found.")));
    [HttpPost("AddRole")]
    public Task<IActionResult> AddRole(string username, [FromBody] RoleDTO role) => ExecuteSafeAsync(async () =>
    { await _service.AddRole(username, role.Name!); return Ok(true); });
    [HttpDelete("RemoveRole")]
    public Task<IActionResult> RemoveRole(string username, [FromBody] RoleDTO role) => ExecuteSafeAsync(async () =>
    { await _service.RemoveRole(username, role.Name!); return Ok(true); });
    [HttpPost("SaveStatus")]
    public Task<IActionResult> SaveStatus(string userId, [FromBody] Dictionary<string, object> statusParams) => ExecuteSafeAsync(async () =>
    { await _service.SaveStatus(userId, statusParams); return Ok(true); });
    [HttpGet("GetLatestStatus")]
    public Task<IActionResult> GetLatestStatus(string userId) => ExecuteSafeAsync(async () =>
        Ok(await _service.GetLatestStatus(userId) ?? throw new KeyNotFoundException("Status not found.")));
    [HttpPost("GetAllStatusesByFileName")]
    public Task<IActionResult> GetAllStatusesByFileName(string userId, string fileName, string fileExtension)
        => ExecuteSafeAsync(async () => Ok(await _service.GetAllStatusesByFileName(userId, fileName, fileExtension)));
    [HttpPost("GetLatestStatusByFileName")]
    public Task<IActionResult> GetLatestStatusByFileName(string userId, string fileName, string fileExtension)
        => ExecuteSafeAsync(async () => Ok(await _service.GetLatestStatusByFileName(userId, fileName, fileExtension)
            ?? throw new KeyNotFoundException("Status not found.")));
    [HttpPost("GetLatestStatusByFileNameAndTime")]
    public Task<IActionResult> GetLatestStatusByFileNameAndTime(string userId, string fileName, string fileExtension, string timeFilter)
        => ExecuteSafeAsync(async () => Ok(await _service.GetLatestStatusByFileNameAndTime(userId, fileName, fileExtension, timeFilter)
            ?? throw new KeyNotFoundException("Status not found.")));
}
