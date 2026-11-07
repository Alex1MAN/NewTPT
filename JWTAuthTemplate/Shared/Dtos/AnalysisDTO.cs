using System.Text.Json;
namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Запрос анализа: метод, матрица данных и параметры.</summary>
public sealed class AnalysisRequest
{
    public string Method { get; set; } = "PCA";
    public double[][] Data { get; set; } = Array.Empty<double[]>();
    public string[]? ColumnNames { get; set; }
    public double[][]? NewData { get; set; }
    public double VarianceThreshold { get; set; } = 0.95;
    /// <summary>Параметры будущих алгоритмов (например, PLS).</summary>
    public Dictionary<string, JsonElement> Options { get; set; } = new();
}
/// <summary>Результат PCA.</summary>
public sealed class PcaResult
{
    public string Method { get; set; } = "PCA";
    public string[] ColumnNames { get; set; } = Array.Empty<string>();
    public int RowCount { get; set; }
    public int ComponentsCount { get; set; }
    public int ComponentsForVarianceThreshold { get; set; }
    public double[][] Loadings { get; set; } = Array.Empty<double[]>();
    public double[][] Scores { get; set; } = Array.Empty<double[]>();
    public double[] ExplainedVarianceRatio { get; set; } = Array.Empty<double>();
    public double[] CumulativeVariance { get; set; } = Array.Empty<double>();
    public double[][]? TransformedData { get; set; }
}
/// <summary>Числовая таблица с именами столбцов.</summary>
public sealed class AnalysisTable
{
    public double[][] Data { get; set; } = Array.Empty<double[]>();
    public string[] ColumnNames { get; set; } = Array.Empty<string>();
}
