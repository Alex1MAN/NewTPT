using Microsoft.AspNetCore.Identity;

namespace JWTAuthTemplate.Application.Models;

/// <summary>Роль пользователя (ASP.NET Core Identity).</summary>
public class ApplicationRole : IdentityRole<string>
{
    public ApplicationRole()
    {
        // Идентификатор назначается при создании объекта.
        Id = Guid.NewGuid().ToString();
    }

    public List<ApplicationUserRole> Users { get; set; } = new();
}
