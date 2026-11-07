namespace JWTAuthTemplate.Application.Models;

/// <summary>Табличное содержимое файла: заголовки и строки в текстовом виде.</summary>
public sealed class TableData
{
    public TableData(IReadOnlyList<string> labels, IReadOnlyList<string[]> rows)
        => (Labels, Rows) = (labels, rows);

    public IReadOnlyList<string> Labels { get; }
    public IReadOnlyList<string[]> Rows { get; }
}
