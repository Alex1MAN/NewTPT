namespace JWTAuthTemplate.Shared.Dtos;

public class UploadChunkRequest
{
    public string UploadId { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
}
