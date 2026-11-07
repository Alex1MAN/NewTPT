namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Документ пользователя с данными текущей версии.</summary>
public class FileReferenceDTO
{
    /// <summary>ETag текущей версии в MinIO.</summary>
    public string FileReferenceMinio { get; set; } = string.Empty;
    public string MinioObjectName { get; set; } = string.Empty;
    /// <summary>ID документа (логического файла).</summary>
    public int DocumentId { get; set; }

    /// <summary>Оригинальное имя файла (например, "report.xlsx").</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Расширение файла без точки (например, "xlsx", "spc").</summary>
    public string FileExtension { get; set; } = string.Empty;

    /// <summary>Номер текущей активной версии.</summary>
    public int CurrentVersionNumber { get; set; }

    /// <summary>Дата создания документа.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата последнего обновления (загрузки новой версии).</summary>
    public DateTime UpdatedAt { get; set; }
}
