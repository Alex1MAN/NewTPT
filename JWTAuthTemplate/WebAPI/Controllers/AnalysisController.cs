using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Validation;
using JWTAuthTemplate.Shared.Dtos;
using JWTAuthTemplate.WebAPI.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Анализ данных (PCA и будущие методы).</summary>
[ApiController]
[Route("analysis")]
public sealed class AnalysisController : BaseController
{
    private const int MaxJsonBytes = 32 * 1024 * 1024;
    private readonly IAnalysisService _analysis;

    public AnalysisController(IAnalysisService analysis) => _analysis = analysis;

    [HttpGet("methods")]
    public IActionResult GetMethods() => Ok(_analysis.GetMethods());

    [HttpPost("Analyze")]
    [RequestSizeLimit(MaxJsonBytes)]
    public Task<IActionResult> Analyze([FromBody] AnalysisRequest request)
        => ExecuteSafeAsync(() => Ok(_analysis.Analyze(request)));

    [HttpPost("PCA")]
    [RequestSizeLimit(MaxJsonBytes)]
    public Task<IActionResult> PCA([FromBody] AnalysisRequest request)
    {
        request.Method = "PCA";
        return Analyze(request);
    }

    [HttpPost("AnalyzeExcel")]
    [RequestSizeLimit(AnalysisLimits.MaxExcelBytes)]
    public Task<IActionResult> AnalyzeExcel(IFormFile file, [FromQuery] string method = "PCA", [FromQuery] double varianceThreshold = 0.95)
        => ExecuteSafeAsync(() => Ok(_analysis.AnalyzeExcel(file.ToFileUpload(), method, varianceThreshold)));

    [HttpPost("AnalyzeStoredExcel")]
    public Task<IActionResult> AnalyzeStoredExcel(string bucketName, string objectName, string method = "PCA", double varianceThreshold = 0.95)
        => ExecuteSafeAsync(async () => Ok(await _analysis.AnalyzeStoredExcelAsync(bucketName, objectName, method, varianceThreshold)));
}
