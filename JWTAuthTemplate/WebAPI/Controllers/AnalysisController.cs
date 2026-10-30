using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
namespace JWTAuthTemplate.WebAPI.Controllers;
[ApiController]
[Route("analysis")]
public sealed class AnalysisController : BaseController
{
    private readonly IAnalysisService _analysis;
    private readonly IMinioService _minio;
    public AnalysisController(IAnalysisService analysis, IMinioService minio) => (_analysis, _minio) = (analysis, minio);
    [HttpGet("methods")]
    public IActionResult GetMethods() => Ok(_analysis.GetMethods());
    [HttpPost("Analyze")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    public Task<IActionResult> Analyze([FromBody] AnalysisRequest request)
        => ExecuteSafeAsync(() => Task.FromResult<IActionResult>(Ok(_analysis.Analyze(request))));
    [HttpPost("PCA")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    public Task<IActionResult> PCA([FromBody] AnalysisRequest request)
    {
        request.Method = "PCA";
        return Analyze(request);
    }
    [HttpPost("AnalyzeExcel")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public Task<IActionResult> AnalyzeExcel(IFormFile file, [FromQuery] string method = "PCA", [FromQuery] double varianceThreshold = 0.95)
        => ExecuteSafeAsync(() =>
        {
            if (file.Length == 0 || !Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A non-empty .xlsx file is required.");
            using var stream = file.OpenReadStream();
            return Task.FromResult<IActionResult>(Ok(_analysis.AnalyzeExcel(stream, method, varianceThreshold)));
        });
    [HttpPost("AnalyzeStoredExcel")]
    public Task<IActionResult> AnalyzeStoredExcel(string bucketName, string objectName, string method = "PCA", double varianceThreshold = 0.95)
        => ExecuteSafeAsync(async () =>
        {
            if (!Path.GetExtension(objectName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("An .xlsx object is required.");
            using var stream = await _minio.GetFileAsync(bucketName, objectName, 16 * 1024 * 1024);
            return Ok(_analysis.AnalyzeExcel(stream, method, varianceThreshold));
        });
}
