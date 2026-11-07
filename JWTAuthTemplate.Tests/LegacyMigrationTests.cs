using System.Text.Json;
using JWTAuthTemplate.Infrastructure.Database;
using JWTAuthTemplate.Tests.LegacyFixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;
namespace JWTAuthTemplate.Tests;

// Разрешение запуска такое же, как у существующих Docker-тестов.
// Создаётся отдельная БД newtpt_legacy_test_<guid>, основная тестовая БД не меняется.
[Trait("Category", "Integration")]
public class LegacyMigrationTests
{
    private static string Repair => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "repair_legacy_database.sql"));
    private static async Task Execute(string connection, string sql)
    {
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, db) { CommandTimeout = 180 };
        await cmd.ExecuteNonQueryAsync();
    }
    private static async Task<T> Scalar<T>(string connection, string sql)
    {
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, db);
        return (T)(await cmd.ExecuteScalarAsync())!;
    }
    private static DbContextOptions<LegacyFixtureContext> LegacyOptions(string connection) =>
        new DbContextOptionsBuilder<LegacyFixtureContext>()
            .UseNpgsql(connection, o => o.MigrationsAssembly(typeof(LegacyFixtureContext).Assembly.FullName)).Options;
    private static Context Current(string connection) => new(new DbContextOptionsBuilder<Context>().UseNpgsql(connection).Options);
    private sealed class TestDatabase : IAsyncDisposable
    {
        public string Connection { get; }
        private readonly string _admin, _name;
        private TestDatabase(string admin, string name, string connection) => (_admin, _name, Connection) = (admin, name, connection);
        public static async Task<TestDatabase> Create()
        {
            var b = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NEWTPT_TEST_DB") ??
                "Host=localhost;Database=JWTAuthTemplate;Username=postgres;Password=root");
            b.Database = "postgres"; b.Pooling = false;
            var admin = b.ConnectionString;
            string name = "newtpt_legacy_test_" + Guid.NewGuid().ToString("N");
            await Execute(admin, $"CREATE DATABASE \"{name}\"");
            b.Database = name;
            return new TestDatabase(admin, name, b.ConnectionString);
        }
        public async ValueTask DisposeAsync()
        {
            await Execute(_admin, $"DROP DATABASE \"{_name}\" WITH (FORCE)");
        }
    }
    private static async Task LegacySeed(string connection)
    {
        using (var legacy = new LegacyFixtureContext(LegacyOptions(connection)))
            await legacy.GetService<IMigrator>().MigrateAsync("20250725153627_Act1");
        await Execute(connection, """
            INSERT INTO "AspNetUsers" ("Id","CreateDate","RefreshTokenExpiryTime","EmailConfirmed",
              "PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount","UserName","PasswordHash")
            VALUES ('legacy-user','2025-01-01Z','2027-01-01Z',false,false,false,false,0,'legacy-name','KEEP_HASH');
            INSERT INTO "AspNetRoles" ("Id","Name","NormalizedName") VALUES ('legacy-role','Legacy','LEGACY');
            INSERT INTO "AspNetUserRoles" ("UserId","RoleId") VALUES ('legacy-user','legacy-role');
            INSERT INTO "UserProfilesMinio" ("UserName","ExternalMinioKey") VALUES ('legacy-name','KEEP_MINIO_KEY');
            INSERT INTO "UserReferencesInMinio" ("UserId","FileName","FileExtension","FileReferenceMinio")
              VALUES ('legacy-user','data.xlsx','xlsx','KEEP_ETAG');
            INSERT INTO "UserSessionStatuses" ("UserId","ActualAt","StatusParams")
              VALUES ('legacy-user','2026-01-02Z','{"fileName":"data.xlsx","fileExtension":"xlsx","step":3}'),
                     ('legacy-user','2026-01-03Z','{"FileName":"other.csv","FileExtension":"csv"}'),
                     ('legacy-user','2026-01-04Z','{"step":4}');
            """);
        using (var legacy = new LegacyFixtureContext(LegacyOptions(connection)))
            await legacy.Database.MigrateAsync();
        await Execute(connection, """
            UPDATE "Documents" SET "CreatedAt"='2026-02-03 12:34:56.123456',"UpdatedAt"='2026-02-03 12:35:00';
            UPDATE "DocumentVersions" SET "CreatedAt"='2026-02-03 12:34:57';
            """);
    }
    private static Task<string> Snapshot(string connection) => Scalar<string>(connection, """
        SELECT jsonb_build_object(
            'users',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "AspNetUsers" t),
            'roles',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "AspNetRoles" t),
            'userRoles',(SELECT jsonb_agg(to_jsonb(t)) FROM "AspNetUserRoles" t),
            'profiles',(SELECT jsonb_agg(to_jsonb(t) ORDER BY "Id") FROM "UserProfilesMinio" t),
            'docs',(SELECT jsonb_agg(to_jsonb(t)-'CreatedAt'-'UpdatedAt' ORDER BY "Id") FROM "Documents" t),
            'versions',(SELECT jsonb_agg(to_jsonb(t)-'CreatedAt' ORDER BY "Id") FROM "DocumentVersions" t),
            'statuses',(SELECT jsonb_agg(to_jsonb(t)-'FileName'-'FileExtension' ORDER BY "Id") FROM "UserSessionStatuses" t)
        )::text
        """);
    [DockerFact]
    public async Task ActualLegacyHistoryRepairedWithoutDataLossAndRepeatIsSafe()
    {
        await using var db = await TestDatabase.Create();
        await LegacySeed(db.Connection);
        string before = await Snapshot(db.Connection);
        Assert.True(await Scalar<bool>(db.Connection, """SELECT to_regclass('public."UserReferencesInMinio"') IS NULL"""));
        // Точно воспроизводим reported failure до исправления.
        using (var current = Current(db.Connection))
        {
            var guard = Assert.Throws<InvalidOperationException>(() => DatabaseMigrationRunner.Migrate(current));
            Assert.Contains("README.md", guard.Message);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => current.Database.MigrateAsync());
            Assert.Equal("42P07", ex.SqlState);
        }
        var sql = Repair; // Тестируем поставляемый скрипт без замены настройки.
        await Execute(db.Connection, sql);
        Assert.Equal(before, await Snapshot(db.Connection));
        Assert.Equal("KEEP_ETAG", await Scalar<string>(db.Connection, """SELECT "FileReferenceMinio" FROM "UserReferencesInMinio" """));
        Assert.Equal("data.xlsx|other.csv|", await Scalar<string>(db.Connection,
            """SELECT string_agg("FileName",'|' ORDER BY "Id") FROM "UserSessionStatuses" """));
        Assert.Equal("2026-02-03 08:34:56.123456", await Scalar<string>(db.Connection,
            """SELECT ("CreatedAt" AT TIME ZONE 'UTC')::text FROM "Documents" """));
        Assert.Equal(4L, await Scalar<long>(db.Connection, """SELECT count(*) FROM "__EFMigrationsHistory" """));
        await Execute(db.Connection, sql);
        Assert.Equal(before, await Snapshot(db.Connection));
        Assert.Equal(1L, await Scalar<long>(db.Connection, """SELECT count(*) FROM "UserReferencesInMinio" """));
        using (var current = Current(db.Connection))
        {
            DatabaseMigrationRunner.Migrate(current);
            var document = await current.Documents.Include(d => d.Versions).SingleAsync();
            Assert.Equal(DateTimeKind.Utc, document.CreatedAt.Kind);
            Assert.Equal("KEEP_ETAG", document.Versions.Single().ETag);
            Assert.Equal(3, await current.UserSessionStatuses.CountAsync());
            Assert.Empty(await current.Database.GetPendingMigrationsAsync());
        }
        Assert.Equal(5L, await Scalar<long>(db.Connection, """SELECT count(*) FROM "__EFMigrationsHistory" """));
        Assert.Equal(before, await Snapshot(db.Connection));
        await Execute(db.Connection, sql); // И после ChunkUploads повтор не дублирует данные.
        Assert.Equal(1L, await Scalar<long>(db.Connection, """SELECT count(*) FROM "UserReferencesInMinio" """));
        Assert.True(await Scalar<bool>(db.Connection, "SELECT to_regnamespace('newtpt_expected_baseline') IS NULL"));
    }
    [DockerFact]
    public async Task MissingTimezoneAbortsBeforeAnyChanges()
    {
        await using var db = await TestDatabase.Create(); await LegacySeed(db.Connection);
        string before = await Snapshot(db.Connection);
        await Assert.ThrowsAsync<PostgresException>(() => Execute(db.Connection,
            Repair.Replace("'Europe/Samara'", "'CHANGE_ME'")));
        Assert.Equal(before, await Snapshot(db.Connection));
        Assert.Equal(3L, await Scalar<long>(db.Connection, """SELECT count(*) FROM "__EFMigrationsHistory" """));
        Assert.False(await Scalar<bool>(db.Connection,
            """SELECT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_name='UserSessionStatuses' AND column_name='FileName')"""));
    }
    [DockerFact]
    public async Task SchemaMismatchRollsBackDatesBackfillAndHistory()
    {
        await using var db = await TestDatabase.Create(); await LegacySeed(db.Connection);
        await Execute(db.Connection, """ALTER TABLE "Documents" ALTER COLUMN "OriginalName" TYPE varchar(250)""");
        string before = await Snapshot(db.Connection);
        await Assert.ThrowsAsync<PostgresException>(() => Execute(db.Connection, Repair.Replace("'Europe/Samara'", "'UTC'")));
        Assert.Equal(before, await Snapshot(db.Connection));
        Assert.Equal(3L, await Scalar<long>(db.Connection, """SELECT count(*) FROM "__EFMigrationsHistory" """));
        Assert.Equal("timestamp without time zone", await Scalar<string>(db.Connection,
            """SELECT data_type FROM information_schema.columns WHERE table_name='Documents' AND column_name='CreatedAt'"""));
        Assert.True(await Scalar<bool>(db.Connection, """SELECT to_regclass('public."UserReferencesInMinio"') IS NULL"""));
    }
    [DockerFact]
    public async Task BrokenCurrentVersionAbortsWithoutGuessingRepair()
    {
        await using var db = await TestDatabase.Create(); await LegacySeed(db.Connection);
        await Execute(db.Connection, """UPDATE "Documents" SET "CurrentVersionId"=987654321""");
        string before = await Snapshot(db.Connection);
        await Assert.ThrowsAsync<PostgresException>(() => Execute(db.Connection, Repair.Replace("'Europe/Samara'", "'UTC'")));
        Assert.Equal(before, await Snapshot(db.Connection));
        Assert.Equal(3L, await Scalar<long>(db.Connection, """SELECT count(*) FROM "__EFMigrationsHistory" """));
    }
    [DockerFact]
    public async Task UnknownHistoryAbortsBeforeChanges()
    {
        await using var db = await TestDatabase.Create(); await LegacySeed(db.Connection);
        await Execute(db.Connection, """INSERT INTO "__EFMigrationsHistory" VALUES ('20260930000000_Unknown','7.0.7')""");
        string before = await Snapshot(db.Connection);
        await Assert.ThrowsAsync<PostgresException>(() => Execute(db.Connection, Repair.Replace("'Europe/Samara'", "'UTC'")));
        Assert.Equal(before, await Snapshot(db.Connection));
        Assert.True(await Scalar<bool>(db.Connection, """SELECT to_regclass('public."UserReferencesInMinio"') IS NULL"""));
    }
    [DockerFact]
    public async Task MissingForeignKeyRollsBackAndDoesNotRecordDevelopInit()
    {
        await using var db = await TestDatabase.Create(); await LegacySeed(db.Connection);
        await Execute(db.Connection, """ALTER TABLE "AspNetUserRoles" DROP CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" """);
        string before = await Snapshot(db.Connection);
        await Assert.ThrowsAsync<PostgresException>(() => Execute(db.Connection, Repair.Replace("'Europe/Samara'", "'UTC'")));
        Assert.Equal(before, await Snapshot(db.Connection));
        Assert.Equal(3L, await Scalar<long>(db.Connection, """SELECT count(*) FROM "__EFMigrationsHistory" """));
    }
    [DockerFact]
    public async Task WrongIndexDefinitionRollsBackInsteadOfTrustingIndexName()
    {
        await using var db = await TestDatabase.Create(); await LegacySeed(db.Connection);
        await Execute(db.Connection, """
            DROP INDEX "IX_DocumentVersions_DocumentId_VersionNumber";
            CREATE INDEX "IX_DocumentVersions_DocumentId_VersionNumber" ON "DocumentVersions" ("DocumentId","VersionNumber");
            """);
        string before = await Snapshot(db.Connection);
        await Assert.ThrowsAsync<PostgresException>(() => Execute(db.Connection, Repair.Replace("'Europe/Samara'", "'UTC'")));
        Assert.Equal(before, await Snapshot(db.Connection));
        Assert.Equal(3L, await Scalar<long>(db.Connection, """SELECT count(*) FROM "__EFMigrationsHistory" """));
    }
    [DockerFact]
    public async Task FreshDatabaseStillMigratesAndRepairRefusesWrongDatabase()
    {
        await using var db = await TestDatabase.Create();
        using var current = Current(db.Connection);
        DatabaseMigrationRunner.Migrate(current);
        Assert.Equal(2L, await Scalar<long>(db.Connection, """SELECT count(*) FROM "__EFMigrationsHistory" """));
        await Assert.ThrowsAsync<PostgresException>(() => Execute(db.Connection, Repair.Replace("'Europe/Samara'", "'UTC'")));
        Assert.Equal(2L, await Scalar<long>(db.Connection, """SELECT count(*) FROM "__EFMigrationsHistory" """));
        DatabaseMigrationRunner.Migrate(current);
    }
    [DockerFact]
    public async Task ExplicitUtcConversionDoesNotDependOnSessionTimeZone()
    {
        await using var db = await TestDatabase.Create(); await LegacySeed(db.Connection);
        var options = new NpgsqlConnectionStringBuilder(db.Connection) { Timezone = "America/New_York" };
        await Execute(options.ConnectionString, Repair.Replace("'Europe/Samara'", "'UTC'"));
        Assert.Equal("2026-02-03 12:34:56.123456", await Scalar<string>(db.Connection,
            """SELECT ("CreatedAt" AT TIME ZONE 'UTC')::text FROM "Documents" """));
    }
}
