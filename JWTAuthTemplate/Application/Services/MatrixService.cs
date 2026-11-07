using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Validation;

namespace JWTAuthTemplate.Application.Services;

/// <summary>Операции над матрицами.</summary>
public sealed class MatrixService : IMatrixService
{
    public double[][] Transpose(double[][] matrix)
    {
        MatrixValidation.Validate(matrix);
        return Enumerable.Range(0, matrix[0].Length)
            .Select(j => matrix.Select(row => row[j]).ToArray()).ToArray();
    }
}
