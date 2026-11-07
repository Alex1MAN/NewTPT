namespace JWTAuthTemplate.Application.Models;

/// <summary>
/// Ссылка на файл пользователя в MinIO (таблица из master-версии).
/// Заполняется при каждой загрузке документа для совместимости со старым клиентом.
/// </summary>
public class UserReferencesInMinio
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string? FileName { get; set; }
    public string? FileExtension { get; set; }
    public string? FileReferenceMinio { get; set; }
}
