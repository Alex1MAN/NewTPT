using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Пользователи и их роли.</summary>
[ApiController]
[Route(ApiRoutes.Prefix + "/users")]
public sealed class UsersController : BaseController
{
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;

    public UsersController(IUserService userService, IRoleService roleService)
        => (_userService, _roleService) = (userService, roleService);

    /// <summary>Пользователь по имени.</summary>
    /// <param name="username">Имя пользователя (логин).</param>
    /// <response code="200">Данные пользователя вместе с ролями.</response>
    /// <response code="404">Пользователь не найден.</response>
    [HttpGet("by-username/{username}")]
    [ProducesResponseType(typeof(UserDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetByUsername(string username) => ExecuteSafeAsync(async () =>
        await _userService.GetByUsername(username) is { } user
            ? Ok(user)
            : Error(StatusCodes.Status404NotFound, $"User with username '{username}' was not found"));

    /// <summary>Роли пользователя.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <response code="200">Имена ролей пользователя.</response>
    /// <response code="404">Пользователь не найден.</response>
    [HttpGet("{userId}/roles")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetUserRoles(string userId)
        => ExecuteSafeAsync(async () => Ok(await _roleService.GetUserRolesAsync(userId)));

    /// <summary>Назначение роли пользователю.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="roleName">Имя существующей роли.</param>
    /// <response code="204">Роль назначена.</response>
    /// <response code="404">Пользователь или роль не найдены.</response>
    /// <response code="409">Роль уже назначена этому пользователю.</response>
    [HttpPut("{userId}/roles/{roleName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> AssignRole(string userId, string roleName) => ExecuteSafeAsync(async () =>
    {
        await _roleService.AssignRoleAsync(userId, roleName);
        return NoContent();
    });

    /// <summary>Снятие роли с пользователя.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="roleName">Имя роли.</param>
    /// <response code="204">Роль снята.</response>
    /// <response code="404">Пользователь или роль не найдены либо роль не была назначена.</response>
    [HttpDelete("{userId}/roles/{roleName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> RemoveRole(string userId, string roleName) => ExecuteSafeAsync(async () =>
    {
        await _roleService.RemoveRoleAsync(userId, roleName);
        return NoContent();
    });
}
