using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace JWTAuthTemplate.Infrastructure.Database;
// Фабрика для инструментов EF (dotnet ef): приложение не запускается и миграции не применяются автоматически.
public sealed class ContextFactory : IDesignTimeDbContextFactory<Context>
{
    public Context CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<Context>()
        .UseNpgsql(Environment.GetEnvironmentVariable("CONNECTION_STRING") ??
            "Host=localhost;Database=JWTAuthTemplate;Username=postgres;Password=root").Options);
}
