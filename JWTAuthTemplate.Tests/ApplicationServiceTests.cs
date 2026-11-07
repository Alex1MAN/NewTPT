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

    private static TableContentService ContentService(Mock<IObjectStorage>? storage = null)
    {
        storage ??= new Mock<IObjectStorage>();
        storage.Setup(s => s.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string bucket, string name, long? max, CancellationToken ct) => System.Text.Encoding.UTF8.GetBytes(name));
        var reader = new Mock<ITableFileReader>();
        reader.Setup(r => r.ReadExcel(It.IsAny<byte[]>())).Returns(Table);
        reader.Setup(r => r.ReadSpc(It.IsAny<byte[]>())).Returns((byte[] b) =>
            new TableData(new[] { "X", "Y" }, new List<string[]> { new[] { System.Text.Encoding.UTF8.GetString(b), "1" } }));
        return new TableContentService(storage.Object, reader.Object);
    }

    private static async Task<string> TableText(TableContentService service, string name, TableRange? range = null)
        => (await service.ReadTablesAsync("u", new[] { name }, range)).Single().Table;

    [Fact]
    public async Task ExcelContentKeepsLegacyJsonTextFormat()
    {
        var json = await TableText(ContentService(), "a.xlsx");
        Assert.Equal(
            "{\n  \"labels\": [\"Name\", \"10\", \"20\", \"30\"],\n  \"values\": [\n    [\n" +
            "      {\"Name\": \"a\\\"b\", \"10\": \"1\", \"20\": \"5\", \"30\": \"x\"},\n" +
            "      {\"Name\": \"c\", \"10\": \"-3\", \"20\": \"2.5\", \"30\": \"9\"}\n" +
            "    ]\n  ]\n}", json);
    }

    [Fact]
    public async Task ExcelRangeFiltersColumnsByXAndClampsNumbersByY()
    {
        // Порядок границ не важен: (25, 5) эквивалентно (5, 25).
        var json = await TableText(ContentService(), "a.xlsx", new TableRange(25, 5, 0, 3));
        Assert.Equal(
            "{\n  \"labels\": [\"10\", \"20\"],\n  \"values\": [\n    [\n" +
            "      {\"10\": \"1\", \"20\": \"3\"},\n" +
            "      {\"10\": \"0\", \"20\": \"2.5\"}\n" +
            "    ]\n  ]\n}", json);
    }

    [Fact]
    public async Task MixedFormatsAreDetectedByExtensionAndKeepOrder()
    {
        var service = ContentService();
        var tables = await service.ReadTablesAsync("u", new[] { "a.spc", "b.SPC", "c.xlsx" }, new TableRange(25, 5, 0, 3));
        Assert.Equal(new[] { "spc", "spc", "xlsx" }, tables.Select(t => t.Format));
        Assert.Equal(new[] { "a.spc", "b.SPC", "c.xlsx" }, tables.Select(t => t.ObjectName));
        Assert.Contains("\"X\": \"a.spc\"", tables[0].Table);
        Assert.Contains("\"X\": \"b.SPC\"", tables[1].Table);
        // Диапазон применяется только к Excel: у SPC остаются оба столбца, у Excel — только "10" и "20".
        Assert.Contains("\"labels\": [\"X\", \"Y\"]", tables[0].Table);
        Assert.Contains("\"labels\": [\"10\", \"20\"]", tables[2].Table);
    }

    [Fact]
    public async Task EmptyListUnknownExtensionAndNonfiniteBoundsRejectedBeforeStorage()
    {
        var storage = new Mock<IObjectStorage>();
        var service = ContentService(storage);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReadTablesAsync("u", Array.Empty<string>()));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReadTablesAsync("u", new[] { "x.bad" }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReadTablesAsync("u", new[] { "a.xlsx", "x.txt" }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReadTablesAsync("u", new[] { "x.xls" }, new TableRange(double.NaN, 0, 0, 0)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReadTablesAsync(" ", new[] { "x.xls" }));
        storage.Verify(s => s.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (DocumentService Service, Mock<IDocumentRepository> Repository, Mock<IObjectStorage> Storage) Documents()
    {
        var repository = new Mock<IDocumentRepository>();
        var storage = new Mock<IObjectStorage>();
        return (new DocumentService(repository.Object, storage.Object, NullLogger<DocumentService>.Instance), repository, storage);
    }

    [Fact]
    public async Task DuplicateFileNameRejectedBeforeUploadUnlessAllowed()
    {
        var (service, repository, storage) = Documents();
        repository.Setup(r => r.UserExistsAsync("u")).ReturnsAsync(true);
        repository.Setup(r => r.GetDocumentsByUserAsync("u")).ReturnsAsync(new List<Document> { new() { OriginalName = "x" } });
        static FileUpload File(string name) => new(name, 1, () => new MemoryStream(new byte[] { 1 }));
        await Assert.ThrowsAsync<ConflictException>(() => service.UploadFilesAsync("u", new[] { File("x") }));
        await Assert.ThrowsAsync<ConflictException>(() => service.UploadFilesAsync("u", new[] { File("y"), File("y") }));
        storage.Verify(s => s.PutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);

        // С флагом allowDuplicateNames проверка имён пропускается и загрузка доходит до хранилища.
        repository.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(Mock.Of<IUnitOfWork>());
        repository.Setup(r => r.AddDocumentAsync("u", "x")).ReturnsAsync(new Document { Id = 2, UserId = "u", OriginalName = "x" });
        repository.Setup(r => r.AddVersionAsync(It.IsAny<Document>(), 1, It.IsAny<string>(), It.IsAny<string>(), 1, "x"))
            .ReturnsAsync(new DocumentVersion { Id = 3, DocumentId = 2, VersionNumber = 1 });
        var result = await service.UploadFilesAsync("u", new[] { File("x") }, allowDuplicateNames: true);
        Assert.Equal(1, result.Count);
        storage.Verify(s => s.PutAsync("u", "u/2/v1_x", It.IsAny<Stream>(), 1, It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task LatestStatusSupportsAnyFileFileAndFileAsOfVariants()
    {
        var repository = new Mock<ISessionStatusRepository>();
        var moment = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        repository.Setup(r => r.GetLatestAsync("u", null, null, null)).ReturnsAsync(new UserSessionStatus { Id = 1, UserId = "u" });
        repository.Setup(r => r.GetLatestAsync("u", "x", "csv", null)).ReturnsAsync(new UserSessionStatus { Id = 2, UserId = "u" });
        repository.Setup(r => r.GetLatestAsync("u", "x", "csv", moment)).ReturnsAsync(new UserSessionStatus { Id = 3, UserId = "u" });
        var service = new SessionService(repository.Object);
        Assert.Equal(1, (await service.GetLatestAsync("u"))!.Id);
        Assert.Equal(2, (await service.GetLatestAsync("u", "x", "csv"))!.Id);
        Assert.Equal(3, (await service.GetLatestAsync("u", "x", "csv", moment))!.Id);
        Assert.Null(await service.GetLatestAsync("u", "none", "csv"));
        // Неполная пара имя/расширение и asOf без файла — ошибки аргументов.
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetLatestAsync("u", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetLatestAsync("u", " ", "csv"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetLatestAsync("u", asOfUtc: moment));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetByFileAsync("u", "x", ""));
        // Первый статус не раньше момента: та же проверка пары имя/расширение.
        repository.Setup(r => r.GetFirstSinceAsync("u", "x", "csv", moment)).ReturnsAsync(new UserSessionStatus { Id = 4, UserId = "u" });
        Assert.Equal(4, (await service.GetFirstSinceAsync("u", "x", "csv", moment))!.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetFirstSinceAsync("u", "x", "", moment));
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
