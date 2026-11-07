namespace JWTAuthTemplate.Shared.Dtos;

/// <summary>Числовая матрица (массив строк одинаковой длины).</summary>
public class MatrixDTO
{
    public required double[][] Data { get; set; }
}
