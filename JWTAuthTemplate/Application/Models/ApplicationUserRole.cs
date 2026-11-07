using Microsoft.AspNetCore.Identity;

namespace JWTAuthTemplate.Application.Models;

/// <summary>Связь пользователя и роли.</summary>
public class ApplicationUserRole : IdentityUserRole<string>
{
    public virtual ApplicationUser? User { get; set; }
    public virtual ApplicationRole? Role { get; set; }
}
