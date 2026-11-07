using System.Text;
using JWTAuthTemplate.Application.Models;

namespace JWTAuthTemplate.Application.Services;

/// <summary>
/// Единый формат JSON-текста таблиц, который ожидает клиент:
/// { "labels": [...], "values": [ [ {"метка": "значение", ...}, ... ] ] }.
/// Формат (отступы, кавычки, значения-строки) сохранён байт-в-байт с прежней реализацией.
/// </summary>
public static class TableJsonFormatter
{
    public static string Format(TableData table)
    {
        var labels = table.Labels.Select(Escape).ToArray();
        var sb = new StringBuilder();
        sb.Append("{\n  \"labels\": [");
        sb.Append(string.Join(", ", labels.Select(l => $"\"{l}\"")));
        sb.Append("],\n  \"values\": [\n    [\n");
        for (int row = 0; row < table.Rows.Count; row++)
        {
            var cells = table.Rows[row];
            sb.Append("      {");
            sb.Append(string.Join(", ", labels.Select((label, col) => $"\"{label}\": \"{Escape(cells[col])}\"")));
            sb.Append('}');
            sb.Append(row < table.Rows.Count - 1 ? ",\n" : "\n");
        }
        sb.Append("    ]\n  ]\n}");
        return sb.ToString();
    }

    private static string Escape(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        return s.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }
}
