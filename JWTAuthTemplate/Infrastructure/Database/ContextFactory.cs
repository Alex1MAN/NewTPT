using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace JWTAuthTemplate.Infrastructure.Database;
// EF tooling не запускает приложение и не применяет миграции во время генерации.
public sealed class ContextFactory : IDesignTimeDbContextFactory<Context>
{
    public Context CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<Context>()
        .UseNpgsql(Environment.GetEnvironmentVariable("CONNECTION_STRING") ??
            "Host=localhost;Database=JWTAuthTemplate;Username=postgres;Password=root").Options);
}
