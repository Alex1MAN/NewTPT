using ClosedXML.Excel;
using MathNet.Numerics.LinearAlgebra;

namespace TestPCA;

public class RawData
{
    public Matrix<double> Data { get; set; } = Matrix<double>.Build.Dense(0, 0);
    public string[] ColumnNames { get; set; } = Array.Empty<string>();
    public int RowCount => Data.RowCount;
    public int ColumnCount => Data.ColumnCount;
}

public class DataProcessor
{
    public RawData LoadDataFromExcel(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return LoadDataFromExcel(stream);
    }

    public RawData LoadDataFromExcel(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);
        var firstRow = worksheet.FirstRowUsed();
        var lastRow = worksheet.LastRowUsed();
        var lastColumn = worksheet.LastColumnUsed();
        if (firstRow is null || lastRow is null || lastColumn is null)
            throw new InvalidOperationException("Лист Excel пуст.");

        int cols = lastColumn.ColumnNumber();
        if (cols > 1024 || lastRow.RowNumber() - firstRow.RowNumber() > 100000 ||
            (long)cols * (lastRow.RowNumber() - firstRow.RowNumber()) > 2_000_000)
            throw new ArgumentException("Excel exceeds analysis limits.");
        var names = new string[cols];
        for (int j = 0; j < cols; j++)
        {
            string name = firstRow.Cell(j + 1).GetString();
            names[j] = string.IsNullOrWhiteSpace(name) ? $"Column{j + 1}" : name;
        }
        var rows = new List<double[]>();
        for (int r = firstRow.RowNumber() + 1; r <= lastRow.RowNumber(); r++)
        {
            var values = new double[cols];
            bool valid = true;
            for (int j = 0; j < cols; j++)
            {
                var cell = worksheet.Cell(r, j + 1);
                if (cell.IsEmpty() || !cell.TryGetValue<double>(out double value)
                    || !double.IsFinite(value))
                {
                    valid = false;
                    break;
                }
                values[j] = value;
            }
            if (valid) rows.Add(values);
        }
        if (rows.Count == 0)
            throw new InvalidOperationException("Нет полных числовых строк для анализа.");
        return new RawData
        {
            Data = Matrix<double>.Build.DenseOfRowArrays(rows.ToArray()),
            ColumnNames = names
        };
    }

    public (Matrix<double> standardized, double[] means, double[] stds)
        Standardize(Matrix<double> data)
    {
        int rows = data.RowCount, cols = data.ColumnCount;
        if (rows < 2 || cols == 0)
            throw new ArgumentException("Для PCA нужны минимум две строки и один столбец.");
        var means = new double[cols];
        var stds = new double[cols];
        var result = Matrix<double>.Build.Dense(rows, cols);
        for (int j = 0; j < cols; j++)
        {
            // Защита постоянных столбцов от погрешности суммирования.
            bool constant = true;
            for (int i = 0; i < rows; i++)
            {
                if (!double.IsFinite(data[i, j])) throw new ArgumentException("Данные содержат NaN или бесконечность.");
                if (data[i, j] != data[0, j]) constant = false;
            }
            if (constant)
            {
                means[j] = data[0, j];
                stds[j] = 0;
                continue;
            }
            for (int i = 0; i < rows; i++)
            {
                if (!double.IsFinite(data[i, j]))
                    throw new ArgumentException("Данные содержат NaN или бесконечность.");
                means[j] += data[i, j];
            }
            means[j] /= rows;
            double sumSq = 0;
            for (int i = 0; i < rows; i++)
            {
                double diff = data[i, j] - means[j];
                sumSq += diff * diff;
            }
            stds[j] = Math.Sqrt(sumSq / rows);
            if (!double.IsFinite(means[j]) || !double.IsFinite(stds[j]))
                throw new ArgumentException("Числовой диапазон данных слишком велик для стандартизации.");
            for (int i = 0; i < rows; i++)
                result[i, j] = stds[j] > 0 ? (data[i, j] - means[j]) / stds[j] : 0;
        }
        return (result, means, stds);
    }
}
