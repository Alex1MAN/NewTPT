namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Тело запроса завершения загрузки по частям.</summary>
public class UploadCompletionDTO
{
    /// <summary>Имя итогового файла (должно совпадать с именем, указанным при загрузке частей).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Общее число частей.</summary>
    public int TotalChunks { get; set; }
}
