using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace JWTAuthTemplate.Infrastructure.Identity;

/// <summary>Единые параметры проверки JWT для аутентификации и обновления токенов.</summary>
public static class JwtValidation
{
    public static SymmetricSecurityKey SigningKey(IConfiguration config)
        => new(Encoding.UTF8.GetBytes(config["JWT:Secret"]
            ?? throw new InvalidOperationException("Не задан JWT:Secret (или переменная окружения JWT_SECRET).")));

    public static TokenValidationParameters Create(IConfiguration config) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = config["JWT:ValidIssuer"],
        ValidateAudience = true,
        ValidAudience = config["JWT:ValidAudience"],
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey(config)
    };
}
