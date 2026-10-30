using JWTAuthTemplate.Application.Interfaces;
namespace JWTAuthTemplate.Application.Services;
public sealed class TestMatrixService : IMatrixService
{
    public double[][] Transpose(double[][] matrix)
    {
        MatrixValidation.Validate(matrix);
        return Enumerable.Range(0, matrix[0].Length)
            .Select(j => matrix.Select(row => row[j]).ToArray()).ToArray();
    }
}
