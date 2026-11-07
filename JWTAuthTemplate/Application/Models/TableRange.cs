namespace JWTAuthTemplate.Application.Models;

/// <summary>
/// Диапазон отбора для табличных данных Excel: остаются столбцы, числовой заголовок которых попадает
/// в [X1; X2], числовые значения ограничиваются отрезком [Y1; Y2]. Порядок границ не важен.
/// </summary>
public sealed record TableRange(double X1, double X2, double Y1, double Y2)
{
    public double MinX => Math.Min(X1, X2);
    public double MaxX => Math.Max(X1, X2);
    public double MinY => Math.Min(Y1, Y2);
    public double MaxY => Math.Max(Y1, Y2);

    /// <summary>Все границы должны быть конечными числами.</summary>
    public bool IsFinite => double.IsFinite(X1) && double.IsFinite(X2) && double.IsFinite(Y1) && double.IsFinite(Y2);
}
