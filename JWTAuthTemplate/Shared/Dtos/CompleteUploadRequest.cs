namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Завершение загрузки по частям.</summary>
public class CompleteUploadRequest
{
    public string UploadId { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
}
