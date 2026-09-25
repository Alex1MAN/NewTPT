namespace JWTAuthTemplate.Shared.Dtos;

public class DocumentVersionDTO
{
    /// <summary>
    /// ID версии
    /// </summary>
    public int VersionId { get; set; }

    /// <summary>
    /// ID родительского документа
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// Номер версии (1, 2, 3...)
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Путь к объекту в MinIO
    /// </summary>
    public string MinioObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Размер файла в байтах
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Оригинальное имя файла на момент загрузки этой версии
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Дата создания версии
    /// </summary>
    public DateTime CreatedAt { get; set; }
}