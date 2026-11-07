namespace JWTAuthTemplate.Application.Models;

/// <summary>
/// Ссылка на файл пользователя в MinIO — историческая таблица master-версии.
/// Сущность сохранена только для схемы БД (миграции) и данных; источник истины — Documents/DocumentVersions.
/// </summary>
public class UserReferencesInMinio
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string? FileName { get; set; }
    public string? FileExtension { get; set; }
    public string? FileReferenceMinio { get; set; }
}
