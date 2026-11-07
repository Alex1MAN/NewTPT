using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Операции над числовыми матрицами.</summary>
[ApiController]
[Route(ApiRoutes.Prefix + "/matrices")]
public sealed class MatricesController : BaseController
{
    private readonly IMatrixService _matrices;

    public MatricesController(IMatrixService matrices) => _matrices = matrices;

    /// <summary>Транспонирование матрицы.</summary>
    /// <remarks>Все строки должны иметь одинаковую длину и содержать конечные числа.</remarks>
    /// <response code="200">Транспонированная матрица.</response>
    /// <response code="400">Матрица пуста, строки разной длины или содержат нечисловые значения.</response>
    [HttpPost("transpose")]
    [ProducesResponseType(typeof(double[][]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Transpose([FromBody] MatrixDTO matrix)
        => ExecuteSafeAsync(() => Ok(_matrices.Transpose(matrix.Data)));
}
