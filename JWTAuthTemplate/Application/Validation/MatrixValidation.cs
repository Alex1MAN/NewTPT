namespace JWTAuthTemplate.Application.Validation;

/// <summary>Общие лимиты размера входных данных анализа (запрос JSON и Excel).</summary>
public static class AnalysisLimits
{
    public const int MaxColumns = 1024;
    public const int MaxRows = 100_000;
    public const long MaxCells = 2_000_000;
    public const long MaxExcelBytes = 16 * 1024 * 1024;

    public static bool Exceeded(long rows, long columns)
        => columns > MaxColumns || rows > MaxRows || rows * columns > MaxCells;
}

/// <summary>Проверка прямоугольности и конечности матрицы.</summary>
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
