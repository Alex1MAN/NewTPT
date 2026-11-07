namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Метаданные одной части файла.</summary>
public class UploadChunkRequest
{
    public string UploadId { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
}
