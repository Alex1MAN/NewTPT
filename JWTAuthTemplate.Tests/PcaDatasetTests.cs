using System.Text.Json;
using JWTAuthTemplate.Infrastructure.Analysis;
using JWTAuthTemplate.Application.Services;
using JWTAuthTemplate.Shared.Dtos;
using MathNet.Numerics.LinearAlgebra;
using TestPCA;
using Xunit;
namespace JWTAuthTemplate.Tests;
public class PcaDatasetTests
{
    [Fact]
    public void SuppliedNiCu100AgreesWithIndependentNumpyReference()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "NiCu100.numpy.json")));
        using var stream = File.OpenRead(Path.Combine(root, "NiCu100.xlsx"));
        var result = (PcaResult)new AnalysisService(new[] { new PcaAnalysisAlgorithm() }, new ExcelAnalysisDataReader())
            .AnalyzeExcel(stream, "PCA", 0.95);
        Assert.Equal(expected.RootElement.GetProperty("rowCount").GetInt32(), result.RowCount);
        Assert.Equal(expected.RootElement.GetProperty("columnCount").GetInt32(), result.ComponentsCount);
        var ratios = expected.RootElement.GetProperty("ratios").EnumerateArray().Select(v => v.GetDouble()).ToArray();
        for (int i = 0; i < ratios.Length; i++)
            Assert.True(Math.Abs(result.ExplainedVarianceRatio[i] - ratios[i]) < 1e-10, $"PC{i + 1}");
    }
    [Fact]
    public void ConstantDecimalColumnIsExactlyZeroAfterStandardization()
    {
        var matrix = Matrix<double>.Build.Dense(100, 1, 0.055);
        var (standardized, means, stds) = new DataProcessor().Standardize(matrix);
        Assert.Equal(0, stds[0]);
        Assert.Equal(0, standardized.FrobeniusNorm());
        Assert.Equal(0.055, means[0]);
    }
}
