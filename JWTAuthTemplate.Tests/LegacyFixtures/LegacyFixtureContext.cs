using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace JWTAuthTemplate.Tests.LegacyFixtures;

// Только воспроизведение исторической БД в отдельной disposable test database.
// Код исторических Up взят из master и присланного пользователем файла.
public sealed class LegacyFixtureContext : DbContext
{
    public LegacyFixtureContext(DbContextOptions<LegacyFixtureContext> options) : base(options) { }
}
[DbContext(typeof(LegacyFixtureContext))]
[Migration("20250719070930_Init")]
public partial class Init { }

[DbContext(typeof(LegacyFixtureContext))]
[Migration("20250725153627_Act1")]
public sealed class Act1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) { }
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
[DbContext(typeof(LegacyFixtureContext))]
[Migration("20260925183255_AddDocumentVersioning")]
public partial class AddDocumentVersioning { }
