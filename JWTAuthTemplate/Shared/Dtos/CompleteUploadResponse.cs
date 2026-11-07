namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Результат сборки файла из частей.</summary>
public class CompleteUploadResponse
{
    public int DocumentId { get; set; }
    public string MinioObjectName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileReferenceMinio { get; set; } = string.Empty;
}
