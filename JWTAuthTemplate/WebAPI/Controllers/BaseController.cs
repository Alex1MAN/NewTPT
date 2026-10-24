using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace JWTAuthTemplate.WebAPI.Controllers;

public abstract class BaseController : ControllerBase
{
    protected async Task<IActionResult> ExecuteRegisterSafeAsync(
    Func<Task<(bool Success, string ErrorMessage)>> action)
    {
        try
        {
            var result = await action();

            if (!result.Success)
                return BadRequest(new { message = result.ErrorMessage });

            return Ok(new { success = true });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception)
        {
            return Problem(statusCode: 500, detail: "¬нутренн€€ ошибка сервера.");
        }
    }

    protected async Task<IActionResult> ExecuteSafeAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return Problem(statusCode: 500, detail: ex.Message);
        }
    }
}
