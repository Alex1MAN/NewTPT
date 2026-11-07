namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Операции над матрицами.</summary>
public interface IMatrixService
{
    double[][] Transpose(double[][] matrix);
}
