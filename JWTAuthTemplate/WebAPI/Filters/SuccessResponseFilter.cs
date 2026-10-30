using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace JWTAuthTemplate.WebAPI.Filters;
// Команды -> true; ответы с данными -> { success: true, data: ... }.
public sealed class SuccessResponseFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is FileResult) context.HttpContext.Response.Headers["X-Success"] = "true";
        if (context.Result is OkResult) context.Result = new OkObjectResult(true);
        else if (context.Result is OkObjectResult ok && ok.Value is not bool)
        {
            bool hasSuccess = ok.Value?.GetType().GetProperties()
                .Any(p => string.Equals(p.Name, "success", StringComparison.OrdinalIgnoreCase) && p.PropertyType == typeof(bool)) == true;
            if (!hasSuccess) ok.Value = ok.Value == null ? true : new { success = true, data = ok.Value };
        }
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
}
