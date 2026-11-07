using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Справочник ролей.</summary>
[ApiController]
[Route(ApiRoutes.Prefix + "/roles")]
public sealed class RolesController : BaseController
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService) => _roleService = roleService;

    /// <summary>Список всех ролей.</summary>
    /// <response code="200">Имена ролей.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetRoles() => ExecuteSafeAsync(async () => Ok(await _roleService.GetAllRolesAsync()));

    /// <summary>Создание роли.</summary>
    /// <response code="201">Роль создана.</response>
    /// <response code="400">Имя роли не указано.</response>
    /// <response code="409">Роль с таким именем уже существует.</response>
    [HttpPost]
    [ProducesResponseType(typeof(RoleDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> CreateRole([FromBody] RoleDTO role) => ExecuteSafeAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(role?.Name))
            return Error(StatusCodes.Status400BadRequest, "Role name is required.");
        return await _roleService.CreateRoleAsync(role.Name)
            ? Created($"/{ApiRoutes.Prefix}/roles/{Uri.EscapeDataString(role.Name)}", new RoleDTO { Name = role.Name })
            : Error(StatusCodes.Status409Conflict, $"Role '{role.Name}' already exists.");
    });

    /// <summary>Удаление роли.</summary>
    /// <param name="roleName">Имя роли.</param>
    /// <response code="204">Роль удалена.</response>
    /// <response code="404">Роль не найдена.</response>
    [HttpDelete("{roleName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DeleteRole(string roleName) => ExecuteSafeAsync(async () =>
        await _roleService.DeleteRoleAsync(roleName) ? NoContent() : Error(StatusCodes.Status404NotFound, $"Role '{roleName}' does not exist."));
}
