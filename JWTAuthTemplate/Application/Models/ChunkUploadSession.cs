namespace JWTAuthTemplate.Application.Models;

/// <summary>Сессия загрузки файла по частям (чанкам).</summary>
public class ChunkUploadSession
{
    /// <summary>SHA-256 от пары "бакет + UploadId" — первичный ключ и ключ блокировки.</summary>
    public string Key { get; set; } = "";
    public string BucketName { get; set; } = "";
    public string UploadId { get; set; } = "";
    public string FileName { get; set; } = "";
    public int TotalChunks { get; set; }
    public bool Completed { get; set; }
    public int? DocumentId { get; set; }
    public string ETag { get; set; } = "";
    public string ObjectName { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<UploadPart> Parts { get; set; } = new();
}

/// <summary>Принятая часть файла в рамках сессии загрузки.</summary>
public class UploadPart
{
    public string SessionKey { get; set; } = "";
    public int ChunkIndex { get; set; }
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
}
