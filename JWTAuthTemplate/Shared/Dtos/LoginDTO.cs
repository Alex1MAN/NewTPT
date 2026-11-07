using System.ComponentModel.DataAnnotations;

namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Данные для входа.</summary>
public class LoginDTO
{
    [Required] public string Username { get; set; } = null!;
    [Required] public string Password { get; set; } = null!;
}
