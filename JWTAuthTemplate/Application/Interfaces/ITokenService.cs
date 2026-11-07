using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Выпуск и обновление JWT и refresh-токенов.</summary>
public interface ITokenService
{
    /// <summary>Выпускает пару токенов и сохраняет refresh-токен пользователю.</summary>
    Task<IssuedTokens> IssueTokensAsync(ApplicationUser user);

    /// <summary>Обменивает (в том числе просроченный) access-токен и refresh-токен на новую пару.</summary>
    Task<RefreshResultDTO> RefreshTokensAsync(string accessToken, string refreshToken);
}

/// <summary>Выпущенная пара токенов.</summary>
public sealed record IssuedTokens(string AccessToken, string RefreshToken, DateTime Expiration);
