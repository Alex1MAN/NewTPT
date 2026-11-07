using System.ComponentModel.DataAnnotations;
using JWTAuthTemplate.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace JWTAuthTemplate.WebAPI.Controllers;

/// <summary>
/// Единое преобразование исключений прикладного слоя в HTTP-ответы. Все ошибки возвращаются в формате
/// ProblemDetails (RFC 7807): { "status": 400, "title": "...", "detail": "сообщение" }.
/// </summary>
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
            return Error(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return Error(StatusCodes.Status404NotFound, ex.Message);
        }
        catch (ConflictException ex)
        {
            return Error(StatusCodes.Status409Conflict, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || HttpContext?.RequestAborted.IsCancellationRequested != true)
        {
            HttpContext?.RequestServices?.GetService<ILogger<BaseController>>()
                ?.LogError(ex, "Необработанная ошибка {Method} {Path}", Request.Method, Request.Path);
            return Error(StatusCodes.Status500InternalServerError, InternalError);
        }
    }

    /// <summary>Синхронный вариант для операций без ввода-вывода.</summary>
    protected Task<IActionResult> ExecuteSafeAsync(Func<IActionResult> action)
        => ExecuteSafeAsync(() => Task.FromResult(action()));

    /// <summary>Регистрация: { success: true } либо 400 с описанием ошибки Identity.</summary>
    protected Task<IActionResult> ExecuteRegisterSafeAsync(Func<Task<(bool Success, string ErrorMessage)>> action)
        => ExecuteSafeAsync(async () =>
        {
            var (success, error) = await action();
            return success ? Ok(new { success = true }) : Error(StatusCodes.Status400BadRequest, error);
        });

    /// <summary>Ответ об ошибке в формате ProblemDetails.</summary>
    protected IActionResult Error(int statusCode, string detail) => Problem(statusCode: statusCode, detail: detail);
}
