using Microsoft.AspNetCore.Identity;

namespace JWTAuthTemplate.Application.Models;

/// <summary>Пользователь системы (ASP.NET Core Identity).</summary>
public class ApplicationUser : IdentityUser<string>
{
    public DateTime CreateDate { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiryTime { get; set; }
    public List<ApplicationUserRole> Roles { get; set; } = new();
}
