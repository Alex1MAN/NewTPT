using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace JWTAuthTemplate.Infrastructure.Identity;

/// <summary>Выпуск JWT и ротация refresh-токенов.</summary>
public sealed class TokenService : ITokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly TokenValidationParameters _validation;
    private readonly ILogger<TokenService> _logger;

    public TokenService(UserManager<ApplicationUser> userManager, IConfiguration configuration,
        TokenValidationParameters validation, ILogger<TokenService> logger)
        => (_userManager, _configuration, _validation, _logger) = (userManager, configuration, validation, logger);

    public async Task<IssuedTokens> IssueTokensAsync(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty)
        };
        return await IssueAsync(user, claims);
    }

    public async Task<RefreshResultDTO> RefreshTokensAsync(string accessToken, string refreshToken)
    {
        var principal = GetPrincipalFromExpiredToken(accessToken)
            ?? throw new SecurityTokenException("Invalid access token.");

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId != null ? await _userManager.FindByIdAsync(userId) : null;
        user ??= principal.Identity?.Name is { } name ? await _userManager.FindByNameAsync(name) : null;
        if (user == null || user.RefreshToken != refreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            throw new SecurityTokenException("Invalid refresh token.");

        var issued = await IssueAsync(user, principal.Claims
            .Where(c => c.Type is not (JwtRegisteredClaimNames.Exp or JwtRegisteredClaimNames.Nbf
                or JwtRegisteredClaimNames.Iat or JwtRegisteredClaimNames.Iss or JwtRegisteredClaimNames.Aud))
            .ToList());

        return new RefreshResultDTO
        {
            Jwt = new JwtDTO { Token = issued.AccessToken, RefreshToken = issued.RefreshToken, Expiration = issued.Expiration },
            User = new RefreshUserDTO
            {
                Id = user.Id,
                Username = user.UserName,
                Email = user.Email,
                Roles = await _userManager.GetRolesAsync(user)
            }
        };
    }

    /// <summary>Создаёт access-токен, новый refresh-токен и сохраняет его пользователю.</summary>
    private async Task<IssuedTokens> IssueAsync(ApplicationUser user, IList<Claim> claims)
    {
        var token = new JwtSecurityToken(
            issuer: _configuration["JWT:ValidIssuer"],
            audience: _configuration["JWT:ValidAudience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(Days("JWT:TokenValidityInDays", 2)),
            signingCredentials: new SigningCredentials(JwtValidation.SigningKey(_configuration), SecurityAlgorithms.HmacSha256));

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(Days("JWT:RefreshTokenValidityInDays", 7));
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
            throw new InvalidOperationException(string.Join(", ", update.Errors.Select(e => e.Description)));

        return new IssuedTokens(new JwtSecurityTokenHandler().WriteToken(token), refreshToken, token.ValidTo);
    }

    /// <summary>Проверяет подпись токена, игнорируя срок действия (для обновления просроченного токена).</summary>
    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;
        var parameters = _validation.Clone();
        parameters.ValidateLifetime = false;
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out var validated);
            return validated is JwtSecurityToken jwt
                && jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase)
                ? principal
                : null;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            _logger.LogWarning(ex, "Не удалось проверить access-токен при обновлении.");
            return null;
        }
    }

    private int Days(string key, int fallback)
        => int.TryParse(_configuration[key], out var days) && days > 0 ? days : fallback;
}
