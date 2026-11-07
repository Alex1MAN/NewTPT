using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Операции с матрицами.</summary>
[ApiController]
[Route("TestMatrix")]
public sealed class MatrixController : BaseController
{
    private readonly IMatrixService _matrixService;

    public MatrixController(IMatrixService matrixService) => _matrixService = matrixService;

    [HttpPost("TransposeMatrix")]
    [HttpPost("/matrices/TransposeMatrix")]
    public Task<IActionResult> TransposeMatrix([FromBody] TestMatrixDTO matrix)
        => ExecuteSafeAsync(() => Ok(new { TransposedMatrix = _matrixService.Transpose(matrix.Data) }));
}
