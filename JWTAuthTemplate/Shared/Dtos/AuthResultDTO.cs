namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Результат входа: JWT, refresh-токен и данные пользователя.</summary>
public class AuthResultDTO
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public UserDTO User { get; set; } = new();
}
