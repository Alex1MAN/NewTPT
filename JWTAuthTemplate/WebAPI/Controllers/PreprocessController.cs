using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Предобработка данных.</summary>
[ApiController]
[Route("TestPreprocess")]
public sealed class PreprocessController : BaseController
{
    private readonly IPreprocessService _service;

    public PreprocessController(IPreprocessService service) => _service = service;

    [HttpPost("process")]
    public Task<IActionResult> ProcessData([FromBody] InputModel input)
        => ExecuteSafeAsync(() => Ok(_service.ProcessData(input)));
}
