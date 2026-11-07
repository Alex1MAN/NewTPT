using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JWTAuthTemplate.WebAPI.Filters;

/// <summary>
/// Единый конверт успешных ответов: команды без данных -> true; ответы с данными (200/201) -> { success: true, data: ... }.
/// Файлы помечаются заголовком X-Success.
/// </summary>
public sealed class SuccessResponseFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        switch (context.Result)
        {
            case FileResult:
                context.HttpContext.Response.Headers["X-Success"] = "true";
                break;
            case OkResult:
                context.Result = new OkObjectResult(true);
                break;
            case ObjectResult { StatusCode: null or StatusCodes.Status200OK or StatusCodes.Status201Created } result
                when result.Value is not bool && !HasSuccessFlag(result.Value):
                result.Value = result.Value == null ? true : new { success = true, data = result.Value };
                break;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context) { }

    private static bool HasSuccessFlag(object? value) => value?.GetType().GetProperties()
        .Any(p => string.Equals(p.Name, "success", StringComparison.OrdinalIgnoreCase) && p.PropertyType == typeof(bool)) == true;
}
