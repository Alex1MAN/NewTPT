using ClosedXML.Excel;
using JWTAuthTemplate.Application.Services;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Infrastructure.Analysis;
using JWTAuthTemplate.Shared.Dtos;
using MathNet.Numerics.LinearAlgebra;

using Moq;
using Xunit;
namespace JWTAuthTemplate.Tests;

public class AnalysisTests
{
    private static readonly double[][] Data = { new double[] { 1, 2 }, new double[] { 2, 4 }, new double[] { 3, 6 } };
    private static AnalysisService Service() => new(new[] { new PcaAnalysisAlgorithm() }, new ExcelAnalysisDataReader(), Mock.Of<IObjectStorage>());
    [Fact]
    public void CorrelatedPcaMatchesAnalyticalSolution()
    {
        var result = (PcaResult)Service().Analyze(new AnalysisRequest { Data = Data, NewData = Data });
        Assert.Equal(2, result.ComponentsCount);
        Assert.Equal(1, result.ComponentsForVarianceThreshold);
        Assert.Equal(1, result.ExplainedVarianceRatio[0], 12);
        Assert.Equal(0, result.ExplainedVarianceRatio[1], 12);
        Assert.Equal(Math.Sqrt(0.5), Math.Abs(result.Loadings[0][0]), 12);
        Assert.Equal(Math.Sqrt(3), Math.Abs(result.Scores[0][0]), 12);
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 2; j++) Assert.Equal(result.Scores[i][j], result.TransformedData![i][j], 12);
    }
    [Fact]
    public void PcaReconstructsStandardizedMatrix()
    {
        var data = new[] { new[] { 1d, 8, 3 }, new[] { 4d, 2, 9 }, new[] { 2d, 4, 7 }, new[] { 6d, 1, 5 } };
        var result = (PcaResult)Service().Analyze(new AnalysisRequest { Data = data });
        var reconstructed = Matrix<double>.Build.DenseOfRowArrays(result.Scores) *
            Matrix<double>.Build.DenseOfRowArrays(result.Loadings).Transpose();
        var standardized = new DataProcessor().Standardize(Matrix<double>.Build.DenseOfRowArrays(data)).standardized;
        Assert.True((reconstructed - standardized).FrobeniusNorm() < 1e-10);
        Assert.Equal(1, result.ExplainedVarianceRatio.Sum(), 12);
    }
    [Fact]
    public void SingleColumnAndConstantColumnSupported()
    {
        var one = (PcaResult)Service().Analyze(new AnalysisRequest { Data = new[] { new[] { 1d }, new[] { 3d } } });
        Assert.Equal(1, one.ComponentsCount);
        var mixed = (PcaResult)Service().Analyze(new AnalysisRequest { Data = new[] { new[] { 1d, 7 }, new[] { 3d, 7 } } });
        Assert.Equal(1, mixed.ExplainedVarianceRatio[0], 12);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void InvalidThresholdRejected(double threshold)
        => Assert.Throws<ArgumentException>(() => Service().Analyze(new AnalysisRequest { Data = Data, VarianceThreshold = threshold }));
    [Fact]
    public void InvalidMatricesRejected()
    {
        foreach (var data in new[] {
            Array.Empty<double[]>(), new[] { new[] { 1d } }, new[] { new[] { 1d }, new[] { 2d, 3 } },
            new[] { new[] { double.NaN }, new[] { 1d } }, new[] { new[] { 1d }, new[] { 1d } } })
            Assert.Throws<ArgumentException>(() => Service().Analyze(new AnalysisRequest { Data = data }));
    }
    [Fact]
    public void InvalidLabelsAndTransformRejected()
    {
        Assert.Throws<ArgumentException>(() => Service().Analyze(new AnalysisRequest { Data = Data, ColumnNames = new[] { "A" } }));
        Assert.Throws<ArgumentException>(() => Service().Analyze(new AnalysisRequest { Data = Data, NewData = new[] { new[] { 1d } } }));
    }
    [Fact]
    public void UnknownAlgorithmRejectedAndExtensionDoesNotRequireControllerChanges()
    {
        Assert.Throws<ArgumentException>(() => Service().Analyze(new AnalysisRequest { Method = "PLS", Data = Data }));
        var service = new AnalysisService(new IAnalysisAlgorithm[] { new PcaAnalysisAlgorithm(), new EchoAlgorithm() }, new ExcelAnalysisDataReader(), Mock.Of<IObjectStorage>());
        Assert.Equal("extended", service.Analyze(new AnalysisRequest { Method = "future", Data = Data }));
    }
    private sealed class EchoAlgorithm : IAnalysisAlgorithm
    {
        public string Method => "future";
        public object Analyze(AnalysisRequest request) => "extended";
    }
    [Fact]
    public void ExcelMatchesMatrixAndSkipsIncompleteRows()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data");
        sheet.Cell(1, 1).Value = "A"; sheet.Cell(1, 2).Value = "B";
        for (int i = 0; i < Data.Length; i++)
            for (int j = 0; j < 2; j++) sheet.Cell(i + 2, j + 1).Value = Data[i][j];
        sheet.Cell(5, 1).Value = "invalid";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var bytes = stream.ToArray();
        var result = (PcaResult)Service().AnalyzeExcel(new FileUpload("data.xlsx", bytes.Length, () => new MemoryStream(bytes)), "PCA", 0.95);
        Assert.Equal(3, result.RowCount);
        Assert.Equal(new[] { "A", "B" }, result.ColumnNames);
        Assert.Equal(1, result.ExplainedVarianceRatio[0], 12);
    }
    [Fact]
    public void MatrixTransposeAndInvalidInput()
    {
        var service = new MatrixService();
        Assert.Equal(new[] { 1d, 2, 3 }, service.Transpose(Data)[0]);
        Assert.Throws<ArgumentException>(() => service.Transpose(Array.Empty<double[]>()));
    }
    [Fact]
    public void PreprocessPreservesPopulationScalingAndOrder()
    {
        var input = new InputModel
        {
            Data = new DataModel { Datax = new() { 1, 2, 3 }, Datay = new() { 3, 4 } },
            Params = new ParamsModel { PrepX = new() { "scaling", "normalization" }, PrepY = new() { "normalization" } }
        };
        var result = new PreprocessService().ProcessData(input);
        Assert.Equal(-1 / Math.Sqrt(2), result.Datax[0], 12);
        Assert.Equal(0.6, result.Datay[0], 12);
        Assert.Equal(new[] { 1d, 2, 3 }, input.Data.Datax);
    }
    [Fact]
    public void PreprocessZeroConstantAndExtremeValues()
    {
        var result = new PreprocessService().ProcessData(new InputModel
        {
            Data = new() { Datax = new() { 7, 7 }, Datay = new() { 1e300, 1e300 } },
            Params = new() { PrepX = new() { "scaling" }, PrepY = new() { "normalization" } }
        });
        Assert.All(result.Datax, v => Assert.Equal(0, v));
        Assert.Equal(1 / Math.Sqrt(2), result.Datay[0], 12);
    }
    [Fact]
    public void PreprocessInvalidInputsRejected()
    {
        Assert.Throws<ArgumentException>(() => new PreprocessService().ProcessData(new InputModel()));
        Assert.Throws<ArgumentException>(() => new PreprocessService().ProcessData(new InputModel {
            Data = new() { Datax = new() { 1 }, Datay = new() { 2 } }, Params = new() { PrepX = new() { "unknown" } } }));
    }
}
