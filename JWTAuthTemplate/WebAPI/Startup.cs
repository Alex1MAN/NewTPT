using JWTAuthTemplate.Application;
using JWTAuthTemplate.Infrastructure;
using JWTAuthTemplate.Infrastructure.Database;
using JWTAuthTemplate.WebAPI.Filters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace JWTAuthTemplate.WebAPI;

/// <summary>Точка входа: сборка зависимостей слоёв и конвейер HTTP.</summary>
public class Startup
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var config = builder.Configuration;

        // Переменные окружения Docker имеют приоритет над appsettings.json.
        config["JWT:Secret"] = Environment.GetEnvironmentVariable("JWT_SECRET") ?? config["JWT:Secret"];
        config["JWT:ValidAudience"] = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? config["JWT:ValidAudience"];
        config["JWT:ValidIssuer"] = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? config["JWT:ValidIssuer"];

        builder.Services.AddCors(options => options.AddPolicy("AllowAll",
            policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(config);

        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<SuccessResponseFilter>();
            // API отвечает только JSON (ошибки — application/problem+json) и файлами; текстовые ответы не используются.
            options.OutputFormatters.RemoveType<StringOutputFormatter>();
        });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddHttpContextAccessor();

        // Схема по умолчанию — JWT; cookie выставляется при входе для браузерного клиента.
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer()
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.LoginPath = "/account/login";
            });
        // Те же параметры проверки, что использует TokenService.
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<TokenValidationParameters>((options, parameters) => options.TokenValidationParameters = parameters);

        if (builder.Environment.IsDevelopment())
            builder.Services.AddSwaggerGen(ConfigureSwagger);

        var app = builder.Build();
        if (config.GetValue("Database:ApplyMigrations", true))
        {
            using var scope = app.Services.CreateScope();
            DatabaseMigrationRunner.Migrate(scope.ServiceProvider.GetRequiredService<Context>());
        }

        app.UseCors("AllowAll");
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
    }

    private static void ConfigureSwagger(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions c)
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "JWTAuthTemplate", Version = "v1" });
        c.MapType<IFormFile>(() => new OpenApiSchema { Type = "string", Format = "binary" });
        // Описания эндпоинтов берутся из XML-комментариев контроллеров (см. GenerateDocumentationFile в .csproj).
        var xmlPath = Path.Combine(AppContext.BaseDirectory, typeof(Startup).Assembly.GetName().Name + ".xml");
        if (File.Exists(xmlPath))
            c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Введите JWT-токен",
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = "Bearer"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                Array.Empty<string>()
            }
        });
    }
}
