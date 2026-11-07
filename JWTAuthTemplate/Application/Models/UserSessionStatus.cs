using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace JWTAuthTemplate.Application.Models;

/// <summary>Сохранённое состояние пользовательской сессии (произвольный JSON).</summary>
public class UserSessionStatus
{
    [Key]
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTime ActualAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "jsonb")]
    public string StatusParams { get; set; } = "{}";

    /// <summary>Параметры статуса в виде словаря (в БД не хранится).</summary>
    [NotMapped]
    public Dictionary<string, object> StatusParamsDict
    {
        get => JsonSerializer.Deserialize<Dictionary<string, object>>(StatusParams) ?? new();
        set => StatusParams = JsonSerializer.Serialize(value);
    }

    /// <summary>Имя файла из параметров статуса — отдельный столбец для фильтрации.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Расширение файла из параметров статуса — отдельный столбец для фильтрации.</summary>
    public string FileExtension { get; set; } = string.Empty;
}
