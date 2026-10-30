using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
namespace JWTAuthTemplate.Application.Services;
public sealed class AnalysisService : IAnalysisService
{
    private readonly IReadOnlyDictionary<string, IAnalysisAlgorithm> _algorithms;
    private readonly IAnalysisDataReader _reader;
    public AnalysisService(IEnumerable<IAnalysisAlgorithm> algorithms, IAnalysisDataReader reader)
    {
        _algorithms = algorithms.ToDictionary(a => a.Method, StringComparer.OrdinalIgnoreCase);
        _reader = reader;
    }
    public IReadOnlyList<string> GetMethods() => _algorithms.Keys.ToArray();
    public object Analyze(AnalysisRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Method) || !_algorithms.TryGetValue(request.Method, out var algorithm))
            throw new ArgumentException("Unsupported analysis method.");
        MatrixValidation.Validate(request.Data, 2);
        if (request.Data[0].Length > 1024 || request.Data.Length > 100000 ||
            (long)request.Data.Length * request.Data[0].Length > 2_000_000)
            throw new ArgumentException("Analysis input exceeds limits (1024 columns, 100000 rows, 2000000 cells).");
        if (request.ColumnNames != null && request.ColumnNames.Length != request.Data[0].Length)
            throw new ArgumentException("ColumnNames count does not match Data.");
        if (request.NewData != null)
        {
            MatrixValidation.Validate(request.NewData);
            if (request.NewData[0].Length != request.Data[0].Length)
                throw new ArgumentException("NewData column count does not match Data.");
            if ((long)request.NewData.Length * request.NewData[0].Length > 2_000_000)
                throw new ArgumentException("NewData exceeds limits.");
        }
        return algorithm.Analyze(request);
    }
    public object AnalyzeExcel(Stream stream, string method, double varianceThreshold)
    {
        var table = _reader.LoadDataFromExcel(stream);
        return Analyze(new AnalysisRequest { Method = method, Data = table.Data, ColumnNames = table.ColumnNames, VarianceThreshold = varianceThreshold });
    }
}
public static class MatrixValidation
{
    public static void Validate(double[][]? data, int minRows = 1)
    {
        if (data == null || data.Length < minRows || data[0] == null || data[0].Length == 0)
            throw new ArgumentException($"Matrix must have at least {minRows} rows and one column.");
        int cols = data[0].Length;
        if (data.Any(row => row == null || row.Length != cols || row.Any(v => !double.IsFinite(v))))
            throw new ArgumentException("Matrix must be rectangular and finite.");
    }
}
