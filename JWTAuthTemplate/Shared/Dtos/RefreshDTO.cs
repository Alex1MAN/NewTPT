namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Запрос обновления токенов (access-токен может быть просроченным).</summary>
public class RefreshDTO
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
}
