namespace JWTAuthTemplate.Shared.Dtos;

public class CompleteUploadRequest
{
    public string UploadId { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
}
