namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Публичные данные пользователя.</summary>
public class UserDTO
{
    public string? Id { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public DateTime? CreateDate { get; set; }
    public List<UserRoleDTO>? Roles { get; set; }
}
