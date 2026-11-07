namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Ответ на обновление токенов.</summary>
public class RefreshResultDTO
{
    public JwtDTO Jwt { get; set; } = new();
    public RefreshUserDTO User { get; set; } = new();
}

/// <summary>Новая пара токенов и срок действия access-токена (UTC).</summary>
public class JwtDTO
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
}

/// <summary>Владелец токена.</summary>
public class RefreshUserDTO
{
    public string Id { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Email { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
}
