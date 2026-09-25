namespace JWTAuthTemplate.Shared.Dtos;

public class CompleteUploadResponse
{
    public bool Success { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileReferenceMinio { get; set; } = string.Empty;
}
