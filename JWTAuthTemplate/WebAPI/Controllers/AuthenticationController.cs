using System.Security.Claims;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Регистрация, вход/выход и обновление токенов.</summary>
[ApiController]
[Route("auth")]
public sealed class AuthenticationController : BaseController
{
    private readonly IUserService _userService;
    private readonly ITokenService _tokenService;
    private readonly IRoleService _roleService;

    public AuthenticationController(IUserService userService, ITokenService tokenService, IRoleService roleService)
        => (_userService, _tokenService, _roleService) = (userService, tokenService, roleService);

    [HttpPost("Register")]
    public Task<IActionResult> Register([FromBody] RegisterDTO dto)
        => ExecuteRegisterSafeAsync(() => _userService.Register(dto));

    [HttpPost("Login")]
    public Task<IActionResult> Login([FromBody] LoginDTO dto) => ExecuteSafeAsync(async () =>
    {
        var authResult = await _userService.Authenticate(dto);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, authResult.User.Id!),
            new Claim(ClaimTypes.Name, authResult.User.Username!)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
        Response.Cookies.Append("refreshToken", authResult.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
        return Ok(authResult);
    });

    [HttpPost("Logout")]
    public Task<IActionResult> Logout() => ExecuteSafeAsync(async () =>
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(true);
    });

    [HttpPost("AddRole")]
    public Task<IActionResult> AddRole([FromBody] RoleDTO role) => ExecuteSafeAsync(async () =>
        await _roleService.CreateRoleAsync(role.Name) ? Ok(true) : Conflict("Role already exists or cannot be created."));

    [HttpGet("GetRoles")]
    public Task<IActionResult> GetRoles() => ExecuteSafeAsync(async () => Ok(await _roleService.GetAllRolesAsync()));

    /// <summary>Обмен access-токена (в том числе просроченного) и refresh-токена на новую пару.</summary>
    [HttpPost("RefreshToken")]
    public Task<IActionResult> RefreshToken([FromBody] RefreshDTO model)
        => ExecuteSafeAsync(async () => Ok(await _tokenService.RefreshTokensAsync(model.AccessToken, model.RefreshToken)));
}
