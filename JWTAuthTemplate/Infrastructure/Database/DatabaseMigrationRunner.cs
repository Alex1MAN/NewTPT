using Microsoft.EntityFrameworkCore;
namespace JWTAuthTemplate.Infrastructure.Database;

/// <summary>Применение миграций при старте с защитой legacy-базы от неверной цепочки миграций.</summary>
public static class DatabaseMigrationRunner
{
    public static void Migrate(Context context)
    {
        // Не помечаем чужую Init выполненной только по факту существования таблиц.
        // Согласование legacy-схемы выполняется отдельно после backup и проверки.
        if (context.Database.CanConnect())
        {
            var applied = context.Database.GetAppliedMigrations().ToHashSet(StringComparer.Ordinal);
            if (applied.Contains("20260925183255_AddDocumentVersioning") &&
                !applied.Contains("20261023213643_Init"))
            {
                throw new InvalidOperationException(
                    "Обнаружена legacy-база с AddDocumentVersioning и другой цепочкой миграций. " +
                    "Автоматический запуск develop Init остановлен для защиты данных. " +
                    "Сделайте резервную копию, прочитайте раздел о миграции в README.md и выполните " +
                    "scripts/repair_legacy_database.sql с явным часовым поясом старых дат. " +
                    "Не удаляйте базу, таблицы или историю миграций.");
            }
        }
        context.Database.Migrate();
    }
}
