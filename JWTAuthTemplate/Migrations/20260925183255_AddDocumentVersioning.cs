using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JWTAuthTemplate.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // === Создаём новые таблицы ===

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    OriginalName = table.Column<string>(type: "text", nullable: false),
                    FileExtension = table.Column<string>(type: "text", nullable: false),
                    CurrentVersionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    MinioObjectName = table.Column<string>(type: "text", nullable: false),
                    ETag = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Индексы
            migrationBuilder.CreateIndex(
                name: "IX_Documents_UserId",
                table: "Documents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OriginalName",
                table: "Documents",
                column: "OriginalName");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_DocumentId_VersionNumber",
                table: "DocumentVersions",
                columns: new[] { "DocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_MinioObjectName",
                table: "DocumentVersions",
                column: "MinioObjectName");

            // === Перенос данных из старой таблицы ===
            // ВАЖНО: в старой таблице UserReferencesInMinio НЕТ поля CreatedAt,
            // поэтому для новых полей используем NOW()

            // 1. Создаём документы (для каждой уникальной комбинации UserId + FileName)
            migrationBuilder.Sql(@"
                INSERT INTO ""Documents"" (""UserId"", ""OriginalName"", ""FileExtension"", ""CreatedAt"", ""UpdatedAt"")
                SELECT DISTINCT 
                    ""UserId"", 
                    ""FileName"", 
                    ""FileExtension"", 
                    NOW(), 
                    NOW()
                FROM ""UserReferencesInMinio""
                WHERE ""UserId"" IS NOT NULL AND ""FileName"" IS NOT NULL
            ");

            // 2. Создаём версии для каждой записи из старой таблицы
            migrationBuilder.Sql(@"
                INSERT INTO ""DocumentVersions"" (""DocumentId"", ""VersionNumber"", ""MinioObjectName"", ""ETag"", ""FileSize"", ""OriginalFileName"", ""CreatedAt"")
                SELECT 
                    d.""Id"",
                    1,
                    urm.""FileName"",
                    COALESCE(urm.""FileReferenceMinio"", ''),
                    0,
                    urm.""FileName"",
                    NOW()
                FROM ""UserReferencesInMinio"" urm
                INNER JOIN ""Documents"" d ON d.""UserId"" = urm.""UserId"" AND d.""OriginalName"" = urm.""FileName""
            ");

            // 3. Обновляем CurrentVersionId для каждого документа
            migrationBuilder.Sql(@"
                UPDATE ""Documents""
                SET ""CurrentVersionId"" = (
                    SELECT ""Id"" 
                    FROM ""DocumentVersions"" 
                    WHERE ""DocumentVersions"".""DocumentId"" = ""Documents"".""Id""
                    ORDER BY ""VersionNumber"" DESC
                    LIMIT 1
                )
            ");

            // 4. Удаляем старую таблицу
            migrationBuilder.DropTable(name: "UserReferencesInMinio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Восстанавливаем старую таблицу (без поля CreatedAt, как было изначально)
            migrationBuilder.CreateTable(
                name: "UserReferencesInMinio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    FileName = table.Column<string>(type: "text", nullable: true),
                    FileExtension = table.Column<string>(type: "text", nullable: true),
                    FileReferenceMinio = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReferencesInMinio", x => x.Id);
                });

            // Переносим данные обратно (только для первой версии)
            migrationBuilder.Sql(@"
                INSERT INTO ""UserReferencesInMinio"" (""UserId"", ""FileName"", ""FileExtension"", ""FileReferenceMinio"")
                SELECT 
                    d.""UserId"",
                    dv.""OriginalFileName"",
                    d.""FileExtension"",
                    dv.""ETag""
                FROM ""DocumentVersions"" dv
                INNER JOIN ""Documents"" d ON d.""Id"" = dv.""DocumentId""
                WHERE dv.""VersionNumber"" = 1
            ");

            // Удаляем новые таблицы
            migrationBuilder.DropTable(name: "DocumentVersions");
            migrationBuilder.DropTable(name: "Documents");
        }
    }
}
