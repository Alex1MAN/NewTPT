using MathNet.Numerics.LinearAlgebra;

namespace TestPCA;

public class PcaModel
{
    private readonly RawData _rawData;
    private double[]? _means;
    private double[]? _stds;
    public Matrix<double> Loadings { get; private set; } = Matrix<double>.Build.Dense(0, 0);
    public Matrix<double> Scores { get; private set; } = Matrix<double>.Build.Dense(0, 0);
    public double[] ExplainedVarianceRatio { get; private set; } = Array.Empty<double>();
    public double[] CumulativeVariance { get; private set; } = Array.Empty<double>();
    public int ComponentsCount { get; private set; }

    public PcaModel(RawData rawData) => _rawData = rawData;

    public void Fit()
    {
        var (standardized, means, stds) = new DataProcessor().Standardize(_rawData.Data);
        var covariance = standardized.TransposeThisAndMultiply(standardized)
            / (standardized.RowCount - 1.0);
        var evd = covariance.Evd(Symmetricity.Symmetric);
        var eigenValues = evd.EigenValues.Select(v => v.Real).ToArray();
        var indices = Enumerable.Range(0, eigenValues.Length)
            .OrderByDescending(i => eigenValues[i]).ToArray();
        var sortedValues = indices.Select(i => Math.Max(0.0, eigenValues[i])).ToArray();
        double total = sortedValues.Sum();
        if (!double.IsFinite(total) || total <= 0)
            throw new InvalidOperationException("PCA невозможен: суммарная дисперсия равна нулю или не конечна.");
        var loadings = Matrix<double>.Build.Dense(evd.EigenVectors.RowCount,
            evd.EigenVectors.ColumnCount, (i, j) => evd.EigenVectors[i, indices[j]]);
        var ratios = sortedValues.Select(v => v / total).ToArray();
        var cumulative = new double[ratios.Length];
        double sum = 0;
        for (int i = 0; i < ratios.Length; i++) cumulative[i] = sum += ratios[i];
        Scores = standardized * loadings;
        Loadings = loadings;
        ExplainedVarianceRatio = ratios;
        CumulativeVariance = cumulative;
        ComponentsCount = ratios.Length;
        _means = means;
        _stds = stds;
    }

    public int GetComponentsForVarianceThreshold(double threshold)
    {
        if (_means is null) throw new InvalidOperationException("Модель не обучена.");
        if (!double.IsFinite(threshold) || threshold <= 0 || threshold > 1)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Порог должен быть в (0, 1].");
        for (int i = 0; i < CumulativeVariance.Length; i++)
            if (CumulativeVariance[i] >= threshold) return i + 1;
        return ComponentsCount;
    }

    public Matrix<double> Transform(Matrix<double> newData)
    {
        if (_means is null || _stds is null)
            throw new InvalidOperationException("Модель не обучена.");
        if (newData.ColumnCount != _means.Length)
            throw new ArgumentException("Количество столбцов не совпадает с обучающими данными.");
        var standardized = Matrix<double>.Build.Dense(newData.RowCount, newData.ColumnCount);
        for (int i = 0; i < newData.RowCount; i++)
            for (int j = 0; j < newData.ColumnCount; j++)
            {
                if (!double.IsFinite(newData[i, j]))
                    throw new ArgumentException("Данные содержат NaN или бесконечность.");
                standardized[i, j] = _stds[j] > 0
                    ? (newData[i, j] - _means[j]) / _stds[j] : 0;
            }
        return standardized * Loadings;
    }
}
