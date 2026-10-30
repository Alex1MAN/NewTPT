using System.Text.Json;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Services;
using JWTAuthTemplate.WebAPI.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace JWTAuthTemplate.Tests;

public class MergeConflictResolutionTests
{
    [Fact]
    public async Task LogoutSignsOutAndReturnsBooleanTrue()
    {
        var authentication = new Mock<IAuthenticationService>(MockBehavior.Strict);
        authentication
            .Setup(service => service.SignOutAsync(
                It.IsAny<HttpContext>(),
                CookieAuthenticationDefaults.AuthenticationScheme,
                It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);
        using var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authentication.Object)
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var controller = new AuthenticationController(
            Mock.Of<IUserService>(), Mock.Of<ITokenService>())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = Assert.IsType<OkObjectResult>(await controller.Logout());

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.True(Assert.IsType<bool>(result.Value));
        authentication.Verify(service => service.SignOutAsync(
            context, CookieAuthenticationDefaults.AuthenticationScheme,
            It.IsAny<AuthenticationProperties>()), Times.Once);
    }

    [Fact]
    public async Task RegistrationUnexpectedErrorUsesReadableRussianMessage()
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddControllers();
        using var services = collection.BuildServiceProvider();
        var controller = new BaseControllerHarness
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services }
            }
        };

        var result = Assert.IsType<ObjectResult>(await controller.RunRegister(
            () => Task.FromException<(bool Success, string ErrorMessage)>(
                new InvalidOperationException("private-database-information"))));
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Equal("Внутренняя ошибка сервера.", problem.Detail);
        Assert.DoesNotContain("private-database-information", JsonSerializer.Serialize(problem));
    }

    [Fact]
    public async Task RegistrationSuccessRetainsSuccessTrue()
    {
        var controller = new BaseControllerHarness();

        var result = Assert.IsType<OkObjectResult>(await controller.RunRegister(
            () => Task.FromResult((true, string.Empty))));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("{\"success\":true}", JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task ChunkUploadConflictStillReturns409()
    {
        var controller = new BaseControllerHarness();

        var result = Assert.IsType<ConflictObjectResult>(await controller.RunSafe(
            () => Task.FromException<IActionResult>(new UploadConflictException("chunk conflict"))));

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("chunk conflict", result.Value);
    }

    private sealed class BaseControllerHarness : BaseController
    {
        public Task<IActionResult> RunRegister(
            Func<Task<(bool Success, string ErrorMessage)>> action)
            => ExecuteRegisterSafeAsync(action);

        public Task<IActionResult> RunSafe(Func<Task<IActionResult>> action)
            => ExecuteSafeAsync(action);
    }
}
