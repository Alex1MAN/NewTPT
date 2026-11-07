using System.Globalization;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;

namespace JWTAuthTemplate.Application.Services;

/// <summary>Чтение Excel/SPC из хранилища и преобразование в JSON-текст таблицы.</summary>
public sealed class FileContentService : IFileContentService
{
    private const int MaxFiles = 1000;
    private readonly IObjectStorage _storage;
    private readonly ITableFileReader _reader;

    public FileContentService(IObjectStorage storage, ITableFileReader reader) => (_storage, _reader) = (storage, reader);

    public async Task<string> GetExcelContentAsync(string bucketName, string objectName)
        => TableJsonFormatter.Format(await ReadExcelAsync(bucketName, objectName));

    public async Task<string> GetExcelContentWithLimitsAsync(string bucketName, string objectName,
        double inputX1, double inputX2, double inputY1, double inputY2)
    {
        var table = await ReadExcelAsync(bucketName, objectName);
        double minX = Math.Min(inputX1, inputX2), maxX = Math.Max(inputX1, inputX2);
        double minY = Math.Min(inputY1, inputY2), maxY = Math.Max(inputY1, inputY2);

        // Оставляем столбцы, чей числовой заголовок попадает в диапазон X.
        var columns = Enumerable.Range(0, table.Labels.Count)
            .Where(i => double.TryParse(table.Labels[i], out var x) && x >= minX && x <= maxX)
            .ToArray();

        // Числовые значения ограничиваем диапазоном Y, остальные оставляем как есть.
        var rows = table.Rows.Select(row => columns.Select(col =>
        {
            var cell = row[col];
            return double.TryParse(cell, out var y)
                ? Math.Clamp(y, minY, maxY).ToString(CultureInfo.CurrentCulture)
                : cell;
        }).ToArray()).ToList();

        return TableJsonFormatter.Format(new TableData(columns.Select(i => table.Labels[i]).ToArray(), rows));
    }

    public async Task<string> GetSpcContentAsync(string bucketName, string objectName)
        => TableJsonFormatter.Format(_reader.ReadSpc(await _storage.ReadAllBytesAsync(bucketName, objectName)));

    public async Task<string> GetTablesFromSeveralSpcAsync(string bucketName, string[] fileNames)
    {
        ValidateFiles(fileNames);
        var results = new List<string>();
        foreach (var name in fileNames)
            results.Add(await GetSpcContentAsync(bucketName, name));
        return "[" + string.Join(",", results) + "]";
    }

    public Task<string> GetAllContentFromFilesAsync(string bucketName, string[] fileNames, string allFilesExtension,
        double inputX1, double inputX2, double inputY1, double inputY2)
    {
        ValidateFiles(fileNames);
        if (new[] { inputX1, inputX2, inputY1, inputY2 }.Any(v => !double.IsFinite(v)))
            throw new ArgumentException("Bounds must be finite.");
        string extension = allFilesExtension?.TrimStart('.').ToLowerInvariant() ?? "";
        bool noLimits = inputX1 == inputX2 && inputX2 == inputY1 && inputY1 == inputY2;
        return extension switch
        {
            "xls" or "xlsx" when noLimits => GetExcelContentAsync(bucketName, fileNames[0]),
            "xls" or "xlsx" => GetExcelContentWithLimitsAsync(bucketName, fileNames[0], inputX1, inputX2, inputY1, inputY2),
            "spc" => GetTablesFromSeveralSpcAsync(bucketName, fileNames),
            _ => throw new ArgumentException("Unknown file extension.")
        };
    }

    private async Task<TableData> ReadExcelAsync(string bucketName, string objectName)
        => _reader.ReadExcel(await _storage.ReadAllBytesAsync(bucketName, objectName));

    private static void ValidateFiles(string[] files)
    {
        if (files == null || files.Length == 0 || files.Length > MaxFiles || files.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Provide 1..1000 file names.");
    }
}
