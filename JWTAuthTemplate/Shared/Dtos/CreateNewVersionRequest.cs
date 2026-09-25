namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>
/// DTO для запроса на создание новой версии документа
/// </summary>
public class CreateNewVersionRequest
{
    /// <summary>
    /// ID документа, для которого создаётся новая версия
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// Новый файл (версия)
    /// </summary>
    public IFormFile File { get; set; } = null!;
}