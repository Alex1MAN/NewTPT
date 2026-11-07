namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Сохранённый статус сессии пользователя.</summary>
public class UserSessionStatusDTO
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime ActualAt { get; set; }
    public Dictionary<string, object>? StatusParamsDict { get; set; }
    public string? FileExtension { get; set; }
    public string? FileName { get; set; }
}
