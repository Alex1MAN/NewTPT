using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Application.Validation;
using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Services;

/// <summary>Выбор алгоритма анализа и общая проверка входных данных.</summary>
public sealed class AnalysisService : IAnalysisService
{
    private readonly IReadOnlyDictionary<string, IAnalysisAlgorithm> _algorithms;
    private readonly IAnalysisDataReader _reader;
    private readonly IObjectStorage _storage;

    public AnalysisService(IEnumerable<IAnalysisAlgorithm> algorithms, IAnalysisDataReader reader, IObjectStorage storage)
    {
        _algorithms = algorithms.ToDictionary(a => a.Method, StringComparer.OrdinalIgnoreCase);
        _reader = reader;
        _storage = storage;
    }

    public IReadOnlyList<string> GetMethods() => _algorithms.Keys.ToArray();

    public object Analyze(AnalysisRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Method) || !_algorithms.TryGetValue(request.Method, out var algorithm))
            throw new ArgumentException("Unsupported analysis method.");
        MatrixValidation.Validate(request.Data, 2);
        if (AnalysisLimits.Exceeded(request.Data.Length, request.Data[0].Length))
            throw new ArgumentException("Analysis input exceeds limits (1024 columns, 100000 rows, 2000000 cells).");
        if (request.ColumnNames != null && request.ColumnNames.Length != request.Data[0].Length)
            throw new ArgumentException("ColumnNames count does not match Data.");
        if (request.NewData != null)
        {
            MatrixValidation.Validate(request.NewData);
            if (request.NewData[0].Length != request.Data[0].Length)
                throw new ArgumentException("NewData column count does not match Data.");
            if ((long)request.NewData.Length * request.NewData[0].Length > AnalysisLimits.MaxCells)
                throw new ArgumentException("NewData exceeds limits.");
        }
        return algorithm.Analyze(request);
    }

    public async Task<object> AnalyzeExcelAsync(ExcelSource source, string method, double varianceThreshold)
    {
        if (source == null || !IsXlsx(source.Name))
            throw new ArgumentException("A non-empty .xlsx file is required.");
        await using var stream = await OpenAsync(source);
        return AnalyzeTable(stream, method, varianceThreshold);
    }

    /// <summary>Открывает поток Excel из любого поддерживаемого источника; размер объекта хранилища ограничен.</summary>
    private async Task<Stream> OpenAsync(ExcelSource source) => source switch
    {
        UploadedExcel { File.Length: > 0 } uploaded => uploaded.File.OpenReadStream(),
        UploadedExcel => throw new ArgumentException("A non-empty .xlsx file is required."),
        StoredExcel stored => new MemoryStream(
            await _storage.ReadAllBytesAsync(stored.Bucket, stored.ObjectName, AnalysisLimits.MaxExcelBytes), writable: false),
        _ => throw new ArgumentException("Unsupported Excel source.")
    };

    private object AnalyzeTable(Stream stream, string method, double varianceThreshold)
    {
        var table = _reader.LoadDataFromExcel(stream);
        return Analyze(new AnalysisRequest
        {
            Method = method, Data = table.Data, ColumnNames = table.ColumnNames, VarianceThreshold = varianceThreshold
        });
    }

    private static bool IsXlsx(string? name)
        => Path.GetExtension(name ?? "").Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
}
