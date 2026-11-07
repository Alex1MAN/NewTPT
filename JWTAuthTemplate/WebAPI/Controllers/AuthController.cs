using System.Security.Claims;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Аутентификация: регистрация, вход, выход и обновление токенов.</summary>
[ApiController]
[Route(ApiRoutes.Prefix + "/auth")]
public sealed class AuthController : BaseController
{
    private readonly IUserService _userService;
    private readonly ITokenService _tokenService;

    public AuthController(IUserService userService, ITokenService tokenService)
        => (_userService, _tokenService) = (userService, tokenService);

    /// <summary>Регистрация нового пользователя.</summary>
    /// <remarks>Создаёт учётную запись и личный бакет в хранилище (имя бакета совпадает с идентификатором пользователя).</remarks>
    /// <response code="200">Пользователь создан: { "success": true }.</response>
    /// <response code="400">Ошибка валидации или имя/почта уже заняты: { "message": "..." }.</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Register([FromBody] RegisterDTO dto)
        => ExecuteRegisterSafeAsync(() => _userService.Register(dto));

    /// <summary>Вход по имени пользователя и паролю.</summary>
    /// <remarks>Возвращает access-токен (JWT), refresh-токен и данные пользователя; дополнительно выставляет cookie-сессию для браузера.</remarks>
    /// <response code="200">Токены и данные пользователя.</response>
    /// <response code="400">Неверные учётные данные.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResultDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

    /// <summary>Выход: завершение cookie-сессии браузера.</summary>
    /// <response code="200">Сессия завершена.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> Logout() => ExecuteSafeAsync(async () =>
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(true);
    });

    /// <summary>Обновление пары токенов.</summary>
    /// <remarks>Принимает access-токен (допускается просроченный) и действующий refresh-токен, возвращает новую пару и данные пользователя.</remarks>
    /// <response code="200">Новые токены.</response>
    /// <response code="400">Токены недействительны или refresh-токен просрочен.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResultDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Refresh([FromBody] RefreshDTO model)
        => ExecuteSafeAsync(async () => Ok(await _tokenService.RefreshTokensAsync(model.AccessToken, model.RefreshToken)));
}
