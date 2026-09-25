namespace JWTAuthTemplate.Shared.Dtos;

public class UploadResultDTO
{
    /// <summary>
    /// Количество успешно обработанных файлов
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Имя бакета (обычно это userId)
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Список загруженных документов
    /// </summary>
    public List<FileReferenceDTO> References { get; set; } = new();
}