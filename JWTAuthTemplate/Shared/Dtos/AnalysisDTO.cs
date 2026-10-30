using System.Text.Json;
namespace JWTAuthTemplate.Shared.Dtos;

public sealed class AnalysisRequest
{
    public string Method { get; set; } = "PCA";
    public double[][] Data { get; set; } = Array.Empty<double[]>();
    public string[]? ColumnNames { get; set; }
    public double[][]? NewData { get; set; }
    public double VarianceThreshold { get; set; } = 0.95;
    // Отдельные параметры будущих алгоритмов, например PLS.
    public Dictionary<string, JsonElement> Options { get; set; } = new();
}
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
public sealed class AnalysisTable
{
    public double[][] Data { get; set; } = Array.Empty<double[]>();
    public string[] ColumnNames { get; set; } = Array.Empty<string>();
}
