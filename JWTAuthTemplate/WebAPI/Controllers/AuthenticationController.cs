using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.DTO.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace JWTAuthTemplate.WebAPI.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthenticationController : BaseController
    {
        private readonly IUserService _userService;
        private readonly ITokenService _tokenService;

        public AuthenticationController(IUserService userService, ITokenService tokenService)
        {
            _userService = userService;
            _tokenService = tokenService;
        }


        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
        {
            return await ExecuteRegisterSafeAsync(() => _userService.Register(dto));
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            return await ExecuteSafeAsync(async () =>
            {
                var authResult = await _userService.Authenticate(dto);
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, authResult.User.Id),
                    new Claim(ClaimTypes.Name, authResult.User.Username),
                };
                var claimsIdentity = new ClaimsIdentity(claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    new AuthenticationProperties { IsPersistent = true });
                Response.Cookies.Append("refreshToken", authResult.RefreshToken,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTimeOffset.UtcNow.AddDays(30)
                    });
                return Ok(authResult);
            });
        }


        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            return await ExecuteSafeAsync(async () =>
            {
                await HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                return Ok(new { message = "Выход выполнен успешно" });
            });
        }



        [HttpPost("RefreshToken")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshDTO model)
        {
            try
            {
                var result = await _tokenService.RefreshTokensAsync(model.AccessToken, model.RefreshToken);
                var response = new
                {
                    JWT = new
                    {
                        Token = new JwtSecurityTokenHandler().WriteToken(result.AccessToken),
                        result.RefreshToken,
                        Expiration = result.AccessToken.ValidTo
                    },
                    User = new
                    {
                        id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value!,
                        username = User.Identity!.Name!,
                        email = User.FindFirst(ClaimTypes.Email)?.Value!,
                        roles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value)
                    }
                };
                return Ok(response);
            }
            catch (SecurityTokenException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
