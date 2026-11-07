namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Таблица, прочитанная из одного объекта хранилища.</summary>
public sealed class TableContentDTO
{
    /// <summary>Имя объекта в бакете.</summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>Формат исходного файла: "xlsx", "xls" или "spc".</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>JSON-текст таблицы вида {"labels": [...], "values": [[{...}, ...]]} (формат клиентского парсера).</summary>
    public string Table { get; set; } = string.Empty;
}
