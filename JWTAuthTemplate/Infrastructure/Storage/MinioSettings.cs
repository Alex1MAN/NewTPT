namespace JWTAuthTemplate.Infrastructure.Storage;

/// <summary>Настройки подключения к MinIO (секция "MinioSettings" конфигурации).</summary>
public class MinioSettings
{
    /// <summary>Адрес MinIO для API (внутри Docker — minio:9000).</summary>
    public string? Endpoint { get; set; }

    /// <summary>Адрес MinIO, доступный клиенту, для временных ссылок (например, localhost:9000).</summary>
    public string? PublicEndpoint { get; set; }

    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
}
