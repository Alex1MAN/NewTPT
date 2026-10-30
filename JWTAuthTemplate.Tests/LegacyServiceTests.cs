using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Services;
using JWTAuthTemplate.Models.Identity;
using JWTAuthTemplate.Shared.Dtos;
using Moq;
using Xunit;
namespace JWTAuthTemplate.Tests;
public class LegacyServiceTests
{
    [Fact]
    public async Task SpcAggregationAndExtensionDispatcherPreserveOrder()
    {
        var storage = new Mock<IMinioService>();
        storage.Setup(s => s.GetSPCFileContentAsJson("user", "a")).ReturnsAsync("{\"x\":1}");
        storage.Setup(s => s.GetSPCFileContentAsJson("user", "b")).ReturnsAsync("{\"x\":2}");
        var service = new FileContentService(storage.Object, Mock.Of<IReferencesRepository>());
        Assert.Equal("[{\"x\":1},{\"x\":2}]", await service.GetTablesFromSeveralSPC("user", new[] { "a", "b" }));
        Assert.Equal("[{\"x\":1},{\"x\":2}]", await service.GetAllContentFromFiles("user", new[] { "a", "b" }, ".SPC", 0, 0, 0, 0));
    }
    [Fact]
    public async Task ExcelDispatcherChoosesFullTableOrBounds()
    {
        var storage = new Mock<IMinioService>();
        storage.Setup(s => s.GetExcelFileContentAsJson("user", "a")).ReturnsAsync("full");
        storage.Setup(s => s.GetExcelFileContentAsJsonWithLimits("user", "a", 10, 20, 0, 1)).ReturnsAsync("bounded");
        var service = new FileContentService(storage.Object, Mock.Of<IReferencesRepository>());
        Assert.Equal("full", await service.GetAllContentFromFiles("user", new[] { "a" }, "xlsx", 0, 0, 0, 0));
        Assert.Equal("bounded", await service.GetAllContentFromFiles("user", new[] { "a" }, "xls", 10, 20, 0, 1));
    }
    [Fact]
    public async Task EmptyFilesUnknownExtensionAndNonfiniteBoundsRejected()
    {
        var service = new FileContentService(Mock.Of<IMinioService>(), Mock.Of<IReferencesRepository>());
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetTablesFromSeveralSPC("u", Array.Empty<string>()));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAllContentFromFiles("u", new[] { "x" }, "bad", 0, 0, 0, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAllContentFromFiles("u", new[] { "x" }, "xls", double.NaN, 0, 0, 0));
    }
    [Fact]
    public async Task DuplicateFileNameRejectedBeforeUpload()
    {
        var storage = new Mock<IMinioService>();
        storage.Setup(s => s.GetDocumentsByUserIdAsync("u")).ReturnsAsync(new List<FileReferenceDTO> { new() { FileName = "x" } });
        var service = new FileContentService(storage.Object, Mock.Of<IReferencesRepository>());
        await Assert.ThrowsAsync<UploadConflictException>(() => service.EnsureUniqueFiles("u", new[] { "x" }));
        await Assert.ThrowsAsync<UploadConflictException>(() => service.EnsureUniqueFiles("u", new[] { "y", "y" }));
        await service.EnsureUniqueFiles("u", new[] { "y" });
    }
    [Fact]
    public async Task LegacyTimeFilterKeepsMasterSemanticsAndValidatesFormat()
    {
        var repository = new Mock<ILegacyUserRepository>();
        var first = new UserSessionStatus { ActualAt = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc),
            StatusParamsDict = new() { ["fileName"] = "x", ["fileExtension"] = "csv" } };
        var second = new UserSessionStatus { ActualAt = first.ActualAt.AddHours(1), StatusParamsDict = first.StatusParamsDict };
        repository.Setup(r => r.GetStatuses("u")).ReturnsAsync(new List<UserSessionStatus> { second, first });
        var service = new LegacyUserService(repository.Object);
        Assert.Same(first, await service.GetLatestStatusByFileNameAndTime("u", "x", "csv", "01.01.2026 00:00:00"));
        Assert.Same(second, await service.GetLatestStatusByFileName("u", "x", "csv"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetLatestStatusByFileNameAndTime("u", "x", "csv", "bad"));
    }
    [Fact]
    public void NewApplicationServicesDoNotDependOnInfrastructure()
    {
        foreach (var service in new[] { typeof(AnalysisService), typeof(ChunkUploadService), typeof(FileContentService), typeof(LegacyUserService) })
            Assert.All(service.GetConstructors().SelectMany(c => c.GetParameters()), p =>
                Assert.True(p.ParameterType.Namespace?.StartsWith("JWTAuthTemplate.Infrastructure") != true));
    }
}
