using JWTAuthTemplate.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Управление ролями и ролями пользователей по ID.</summary>
[ApiController]
[Route("roles")]
public sealed class RolesController : BaseController
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService) => _roleService = roleService;

    [HttpGet("get-all")]
    public Task<IActionResult> GetAllRoles() => ExecuteSafeAsync(async () => Ok(await _roleService.GetAllRolesAsync()));

    [HttpPost("create-{roleName}")]
    public Task<IActionResult> CreateRole(string roleName) => ExecuteSafeAsync(async () =>
        await _roleService.CreateRoleAsync(roleName)
            ? CreatedAtAction(nameof(GetAllRoles), null)
            : Conflict($"Role '{roleName}' already exists."));

    [HttpDelete("delete-{roleName}")]
    public Task<IActionResult> DeleteRole(string roleName) => ExecuteSafeAsync(async () =>
        await _roleService.DeleteRoleAsync(roleName) ? NoContent() : NotFound($"Role '{roleName}' does not exist."));

    [HttpPut("assign-user-role/{userId}/{roleName}")]
    public Task<IActionResult> AssignRoleToUser(Guid userId, string roleName) => ExecuteSafeAsync(async () =>
    {
        await _roleService.AssignRoleAsync(userId.ToString(), roleName);
        return Ok(true);
    });

    [HttpDelete("remove-user-role/{userId}/{roleName}")]
    public Task<IActionResult> RemoveRoleFromUser(Guid userId, string roleName) => ExecuteSafeAsync(async () =>
    {
        await _roleService.RemoveRoleAsync(userId.ToString(), roleName);
        return Ok(true);
    });

    [HttpGet("user-roles/{userId}")]
    public Task<IActionResult> GetUserRoles(Guid userId)
        => ExecuteSafeAsync(async () => Ok(await _roleService.GetUserRolesAsync(userId.ToString())));
}
