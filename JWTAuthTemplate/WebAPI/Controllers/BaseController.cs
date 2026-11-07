using System.ComponentModel.DataAnnotations;
using JWTAuthTemplate.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>Единое преобразование исключений прикладного слоя в HTTP-ответы.</summary>
public abstract class BaseController : ControllerBase
{
    private const string InternalError = "Внутренняя ошибка сервера.";

    /// <summary>
    /// 400 — ошибки валидации и аргументов, 404 — объект не найден, 409 — конфликт,
    /// 500 — прочие ошибки (детали только в логе).
    /// </summary>
    protected async Task<IActionResult> ExecuteSafeAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is ValidationException or ArgumentException or SecurityTokenException)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ConflictException ex)
        {
            return Conflict(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || HttpContext?.RequestAborted.IsCancellationRequested != true)
        {
            HttpContext?.RequestServices?.GetService<ILogger<BaseController>>()
                ?.LogError(ex, "Необработанная ошибка {Method} {Path}", Request.Method, Request.Path);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: InternalError);
        }
    }

    /// <summary>Синхронный вариант для операций без ввода-вывода.</summary>
    protected Task<IActionResult> ExecuteSafeAsync(Func<IActionResult> action)
        => ExecuteSafeAsync(() => Task.FromResult(action()));

    /// <summary>Регистрация: { success: true } или 400 { message }.</summary>
    protected Task<IActionResult> ExecuteRegisterSafeAsync(Func<Task<(bool Success, string ErrorMessage)>> action)
        => ExecuteSafeAsync(async () =>
        {
            try
            {
                var (success, error) = await action();
                return success ? Ok(new { success = true }) : BadRequest(new { message = error });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        });
}
