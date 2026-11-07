using Microsoft.AspNetCore.Identity;

namespace JWTAuthTemplate.Application.Models;

/// <summary>Сущность Identity без дополнительных полей.</summary>
public class ApplicationUserLogin : IdentityUserLogin<string>
{
}
