using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using MathNet.Numerics.LinearAlgebra;
using TestPCA;
namespace JWTAuthTemplate.Infrastructure.Analysis;
public sealed class PcaAnalysisAlgorithm : IAnalysisAlgorithm
{
    public string Method => "PCA";
    public object Analyze(AnalysisRequest request)
    {
        if (request.Options.Count != 0) throw new ArgumentException("PCA does not accept additional Options.");
        if (!double.IsFinite(request.VarianceThreshold) || request.VarianceThreshold <= 0 || request.VarianceThreshold > 1)
            throw new ArgumentException("VarianceThreshold must be in (0, 1].");
        var raw = new RawData
        {
            Data = Matrix<double>.Build.DenseOfRowArrays(request.Data),
            ColumnNames = request.ColumnNames ?? Enumerable.Range(1, request.Data[0].Length).Select(i => $"Column{i}").ToArray()
        };
        var model = new PcaModel(raw);
        try { model.Fit(); }
        catch (InvalidOperationException ex) { throw new ArgumentException(ex.Message, ex); }
        return new PcaResult
        {
            ColumnNames = raw.ColumnNames, RowCount = raw.RowCount,
            ComponentsCount = model.ComponentsCount,
            ComponentsForVarianceThreshold = model.GetComponentsForVarianceThreshold(request.VarianceThreshold),
            Loadings = model.Loadings.ToRowArrays(), Scores = model.Scores.ToRowArrays(),
            ExplainedVarianceRatio = model.ExplainedVarianceRatio, CumulativeVariance = model.CumulativeVariance,
            TransformedData = request.NewData == null ? null : model.Transform(Matrix<double>.Build.DenseOfRowArrays(request.NewData)).ToRowArrays()
        };
    }
}
public sealed class ExcelAnalysisDataReader : IAnalysisDataReader
{
    public AnalysisTable LoadDataFromExcel(Stream stream)
    {
        try
        {
            var raw = new DataProcessor().LoadDataFromExcel(stream);
            return new AnalysisTable { Data = raw.Data.ToRowArrays(), ColumnNames = raw.ColumnNames };
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not OutOfMemoryException)
        {
            throw new ArgumentException("Cannot read numeric Excel data: " + ex.Message, ex);
        }
    }
}
