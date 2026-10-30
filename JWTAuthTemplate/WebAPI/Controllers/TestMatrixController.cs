using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.DTO.Identity;
using Microsoft.AspNetCore.Mvc;
namespace JWTAuthTemplate.WebAPI.Controllers;
[ApiController]
[Route("TestMatrix")]
public sealed class TestMatrixController : BaseController
{
    private readonly IMatrixService _matrixService;
    public TestMatrixController(IMatrixService matrixService) => _matrixService = matrixService;
    [HttpPost("TransposeMatrix")]
    [HttpPost("/matrices/TransposeMatrix")]
    public Task<IActionResult> TransposeMatrix([FromBody] TestMatrixDTO matrixDTO)
        => ExecuteSafeAsync(() => Task.FromResult<IActionResult>(Ok(new { TransposedMatrix = _matrixService.Transpose(matrixDTO.Data) })));
}
