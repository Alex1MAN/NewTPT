using System.Globalization;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Services;

/// <summary>Единый сценарий чтения Excel/SPC из хранилища: формат по расширению, необязательный диапазон для Excel.</summary>
public sealed class TableContentService : ITableContentService
{
    private const int MaxFiles = 1000;
    private static readonly string[] ExcelFormats = { "xls", "xlsx" };
    private const string SpcFormat = "spc";

    private readonly IObjectStorage _storage;
    private readonly ITableFileReader _reader;

    public TableContentService(IObjectStorage storage, ITableFileReader reader) => (_storage, _reader) = (storage, reader);

    public async Task<IReadOnlyList<TableContentDTO>> ReadTablesAsync(string bucket, IReadOnlyList<string> objectNames, TableRange? range = null)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentException("Bucket is required.");
        if (objectNames == null || objectNames.Count == 0 || objectNames.Count > MaxFiles || objectNames.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Provide 1..1000 object names.");
        if (range is { IsFinite: false })
            throw new ArgumentException("Bounds must be finite.");

        // Сначала проверяем все форматы, чтобы не читать файлы из хранилища при ошибке в списке.
        var formats = objectNames.Select(FormatOf).ToArray();

        var result = new List<TableContentDTO>(objectNames.Count);
        for (int i = 0; i < objectNames.Count; i++)
        {
            var bytes = await _storage.ReadAllBytesAsync(bucket, objectNames[i]);
            var table = formats[i] == SpcFormat ? _reader.ReadSpc(bytes) : _reader.ReadExcel(bytes);
            if (range != null && formats[i] != SpcFormat)
                table = ApplyRange(table, range);
            result.Add(new TableContentDTO { ObjectName = objectNames[i], Format = formats[i], Table = TableJsonFormatter.Format(table) });
        }
        return result;
    }

    private static string FormatOf(string objectName)
    {
        var extension = Path.GetExtension(objectName).TrimStart('.').ToLowerInvariant();
        if (extension == SpcFormat || ExcelFormats.Contains(extension))
            return extension;
        throw new ArgumentException($"Unsupported file format '{extension}' for '{objectName}'. Supported: xls, xlsx, spc.");
    }

    /// <summary>Оставляет столбцы с числовым заголовком из диапазона X и ограничивает числовые значения диапазоном Y.</summary>
    private static TableData ApplyRange(TableData table, TableRange range)
    {
        var columns = Enumerable.Range(0, table.Labels.Count)
            .Where(i => double.TryParse(table.Labels[i], out var x) && x >= range.MinX && x <= range.MaxX)
            .ToArray();

        var rows = table.Rows.Select(row => columns.Select(col =>
        {
            var cell = row[col];
            return double.TryParse(cell, out var y)
                ? Math.Clamp(y, range.MinY, range.MaxY).ToString(CultureInfo.CurrentCulture)
                : cell;
        }).ToArray()).ToList();

        return new TableData(columns.Select(i => table.Labels[i]).ToArray(), rows);
    }
}
