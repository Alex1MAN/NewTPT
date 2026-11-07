using System.Reflection;
using JWTAuthTemplate.Application.Exceptions;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace JWTAuthTemplate.Tests;

/// <summary>Сервисы прикладного слоя на заглушках портов (без БД и MinIO).</summary>
public class ApplicationServiceTests
{
    private static readonly TableData Table = new(
        new[] { "Name", "10", "20", "30" },
        new List<string[]> { new[] { "a\"b", "1", "5", "x" }, new[] { "c", "-3", "2.5", "9" } });

    private static FileContentService ContentService(Mock<IObjectStorage>? storage = null)
    {
        storage ??= new Mock<IObjectStorage>();
        storage.Setup(s => s.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string bucket, string name, long? max, CancellationToken ct) => System.Text.Encoding.UTF8.GetBytes(name));
        var reader = new Mock<ITableFileReader>();
        reader.Setup(r => r.ReadExcel(It.IsAny<byte[]>())).Returns(Table);
        reader.Setup(r => r.ReadSpc(It.IsAny<byte[]>())).Returns((byte[] b) =>
            new TableData(new[] { "X", "Y" }, new List<string[]> { new[] { System.Text.Encoding.UTF8.GetString(b), "1" } }));
        return new FileContentService(storage.Object, reader.Object);
    }

    [Fact]
    public async Task ExcelContentKeepsLegacyJsonTextFormat()
    {
        var json = await ContentService().GetExcelContentAsync("u", "a.xlsx");
        Assert.Equal(
            "{\n  \"labels\": [\"Name\", \"10\", \"20\", \"30\"],\n  \"values\": [\n    [\n" +
            "      {\"Name\": \"a\\\"b\", \"10\": \"1\", \"20\": \"5\", \"30\": \"x\"},\n" +
            "      {\"Name\": \"c\", \"10\": \"-3\", \"20\": \"2.5\", \"30\": \"9\"}\n" +
            "    ]\n  ]\n}", json);
    }

    [Fact]
    public async Task ExcelLimitsFilterColumnsByXAndClampNumbersByY()
    {
        var json = await ContentService().GetExcelContentWithLimitsAsync("u", "a.xlsx", 25, 5, 0, 3);
        Assert.Equal(
            "{\n  \"labels\": [\"10\", \"20\"],\n  \"values\": [\n    [\n" +
            "      {\"10\": \"1\", \"20\": \"3\"},\n" +
            "      {\"10\": \"0\", \"20\": \"2.5\"}\n" +
            "    ]\n  ]\n}", json);
    }

    [Fact]
    public async Task SpcAggregationAndExtensionDispatcherPreserveOrder()
    {
        var service = ContentService();
        var several = await service.GetTablesFromSeveralSpcAsync("u", new[] { "a", "b" });
        Assert.True(several.IndexOf("\"X\": \"a\"") < several.IndexOf("\"X\": \"b\""));
        Assert.StartsWith("[{", several);
        Assert.Equal(several, await service.GetAllContentFromFilesAsync("u", new[] { "a", "b" }, ".SPC", 0, 0, 0, 0));
        Assert.Equal(await service.GetExcelContentAsync("u", "a"),
            await service.GetAllContentFromFilesAsync("u", new[] { "a" }, "xlsx", 0, 0, 0, 0));
    }

    [Fact]
    public async Task EmptyFilesUnknownExtensionAndNonfiniteBoundsRejected()
    {
        var service = ContentService();
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetTablesFromSeveralSpcAsync("u", Array.Empty<string>()));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAllContentFromFilesAsync("u", new[] { "x" }, "bad", 0, 0, 0, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAllContentFromFilesAsync("u", new[] { "x" }, "xls", double.NaN, 0, 0, 0));
    }

    private static (DocumentService Service, Mock<IDocumentRepository> Repository, Mock<IObjectStorage> Storage) Documents()
    {
        var repository = new Mock<IDocumentRepository>();
        var storage = new Mock<IObjectStorage>();
        return (new DocumentService(repository.Object, storage.Object, NullLogger<DocumentService>.Instance), repository, storage);
    }

    [Fact]
    public async Task DuplicateFileNameRejectedBeforeUpload()
    {
        var (service, repository, _) = Documents();
        repository.Setup(r => r.GetDocumentsByUserAsync("u")).ReturnsAsync(new List<Document> { new() { OriginalName = "x" } });
        await Assert.ThrowsAsync<ConflictException>(() => service.EnsureUniqueFilesAsync("u", new[] { "x" }));
        await Assert.ThrowsAsync<ConflictException>(() => service.EnsureUniqueFilesAsync("u", new[] { "y", "y" }));
        await service.EnsureUniqueFilesAsync("u", new[] { "y" });
    }

    [Fact]
    public async Task LastVersionCannotBeDeletedAndMissingObjectsAreNotFound()
    {
        var (service, repository, storage) = Documents();
        var document = new Document { Id = 1, UserId = "u", OriginalName = "a.xlsx" };
        repository.Setup(r => r.GetVersionAsync(5)).ReturnsAsync(new DocumentVersion { Id = 5, DocumentId = 1, Document = document });
        repository.Setup(r => r.CountVersionsAsync(1)).ReturnsAsync(1);
        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteVersionAsync(5));
        storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetDocumentAsync(404));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetVersionAsync(404));
    }

    [Fact]
    public async Task UploadToUnknownUserRejectedBeforeStorage()
    {
        var (service, repository, storage) = Documents();
        repository.Setup(r => r.UserExistsAsync("u")).ReturnsAsync(false);
        var file = new FileUpload("a.xlsx", 1, () => new MemoryStream(new byte[] { 1 }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UploadFilesAsync("u", new[] { file }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UploadFilesAsync("u", Array.Empty<FileUpload>()));
        storage.Verify(s => s.PutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SessionTimeFiltersUseBothSemanticsAndFileColumns()
    {
        var repository = new Mock<ISessionStatusRepository>();
        var moment = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var since = new UserSessionStatus { Id = 1, UserId = "u", ActualAt = moment.AddMinutes(1), FileName = "x", FileExtension = "csv" };
        var asOf = new UserSessionStatus { Id = 2, UserId = "u", ActualAt = moment.AddMinutes(-1), FileName = "x", FileExtension = "csv" };
        repository.Setup(r => r.GetFirstSinceAsync("u", "x", "csv", moment)).ReturnsAsync(since);
        repository.Setup(r => r.GetLatestAsync("u", "x", "csv", moment)).ReturnsAsync(asOf);
        var service = new SessionService(repository.Object);
        Assert.Equal(1, (await service.GetFirstByFileSinceAsync("u", "x", "csv", moment))!.Id);
        Assert.Equal(2, (await service.GetLatestByFileAsOfAsync("u", "x", "csv", moment))!.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetLatestByFileAsync("u", " ", "csv"));
    }

    [Fact]
    public async Task SavingStatusExtractsFileColumnsAndChecksUser()
    {
        var repository = new Mock<ISessionStatusRepository>();
        UserSessionStatus? saved = null;
        repository.Setup(r => r.UserExistsAsync("u")).ReturnsAsync(true);
        repository.Setup(r => r.AddAsync(It.IsAny<UserSessionStatus>())).Callback<UserSessionStatus>(s => saved = s).ReturnsAsync(7);
        var service = new SessionService(repository.Object);
        Assert.Equal(7, await service.SaveStatusAsync("u", new() { ["FileName"] = "x", ["fileExtension"] = "csv" }));
        Assert.Equal(("x", "csv"), (saved!.FileName, saved.FileExtension));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SaveStatusAsync("other", new() { ["a"] = 1 }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveStatusAsync("u", new()));
    }

    /// <summary>Чистая архитектура: прикладной слой не зависит от инфраструктуры, EF Core, MinIO и ASP.NET Core.</summary>
    [Fact]
    public void ApplicationLayerDoesNotDependOnOuterLayers()
    {
        string[] forbidden = { "JWTAuthTemplate.Infrastructure", "JWTAuthTemplate.WebAPI", "Microsoft.EntityFrameworkCore", "Minio", "Microsoft.AspNetCore.Http", "Microsoft.AspNetCore.Mvc" };
        var types = typeof(DocumentService).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("JWTAuthTemplate.Application") == true);
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var type in types)
        {
            var used = type.GetConstructors(all).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
                .Concat(type.GetFields(all).Select(f => f.FieldType))
                .Concat(type.GetProperties(all).Select(p => p.PropertyType))
                .Concat(type.GetMethods(all).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)))
                .SelectMany(Flatten);
            foreach (var dependency in used)
                Assert.False(forbidden.Any(f => dependency.Namespace?.StartsWith(f) == true),
                    $"{type.FullName} зависит от {dependency.FullName}");
        }
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        if (type.HasElementType)
            foreach (var t in Flatten(type.GetElementType()!)) yield return t;
        if (type.IsGenericType)
            foreach (var t in type.GetGenericArguments().SelectMany(Flatten)) yield return t;
    }
}
