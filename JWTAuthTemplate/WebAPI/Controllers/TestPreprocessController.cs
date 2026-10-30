using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
namespace JWTAuthTemplate.WebAPI.Controllers;
[ApiController]
[Route("TestPreprocess")]
public sealed class TestPreprocessController : BaseController
{
    private readonly IPreprocessService _service;
    public TestPreprocessController(IPreprocessService service) => _service = service;
    [HttpPost("process")]
    public Task<IActionResult> ProcessData([FromBody] InputModel input)
        => ExecuteSafeAsync(() => Task.FromResult<IActionResult>(Ok(_service.ProcessData(input))));
}
