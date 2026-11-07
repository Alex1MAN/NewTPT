using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using MathNet.Numerics.LinearAlgebra;
namespace JWTAuthTemplate.Infrastructure.Analysis;

/// <summary>Алгоритм анализа главных компонент (PCA).</summary>
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
