using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Infrastructure.Analysis;
using JWTAuthTemplate.Infrastructure.Database;
using JWTAuthTemplate.Infrastructure.FileParsing;
using JWTAuthTemplate.Infrastructure.Identity;
using JWTAuthTemplate.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Minio;

namespace JWTAuthTemplate.Infrastructure;

/// <summary>Регистрация реализаций портов прикладного слоя (БД, MinIO, Identity, разбор файлов, анализ).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<Context>(options => options.UseNpgsql(connectionString));

        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<Context>()
            .AddDefaultTokenProviders();

        services.Configure<MinioSettings>(configuration.GetSection("MinioSettings"));
        services.AddSingleton<IMinioClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MinioSettings>>().Value;
            return new MinioClient().WithEndpoint(settings.Endpoint)
                .WithCredentials(settings.AccessKey, settings.SecretKey).Build();
        });
        services.AddScoped<IObjectStorage, MinioObjectStorage>();

        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IUploadRepository, UploadRepository>();
        services.AddScoped<ISessionStatusRepository, SessionStatusRepository>();

        services.AddSingleton<ITableFileReader, TableFileReader>();
        services.AddScoped<IAnalysisAlgorithm, PcaAnalysisAlgorithm>();
        services.AddScoped<IAnalysisDataReader, ExcelAnalysisDataReader>();

        services.AddSingleton(sp => JwtValidation.Create(sp.GetRequiredService<IConfiguration>()));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        return services;
    }
}
