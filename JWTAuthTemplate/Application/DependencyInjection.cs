using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JWTAuthTemplate.Application;

/// <summary>Регистрация сервисов прикладного слоя.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ITableContentService, TableContentService>();
        services.AddScoped<IChunkUploadService, ChunkUploadService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddScoped<IMatrixService, MatrixService>();
        services.AddScoped<IPreprocessService, PreprocessService>();
        return services;
    }
}
