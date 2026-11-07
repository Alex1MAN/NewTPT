using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JWTAuthTemplate.WebAPI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace JWTAuthTemplate.Tests;
public sealed class ApiFactory : WebApplicationFactory<Startup>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseEnvironment("Development").ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ApplyMigrations"] = "false" }));
}
public class HttpContractTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    public HttpContractTests(ApiFactory factory) => _client = factory.CreateClient();
    [Fact]
    public async Task PcaReturnsSuccessTrueAndNumericData()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/analysis", new { data = new[] { new[] { 1d }, new[] { 2d } } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal(1, json.GetProperty("data").GetProperty("componentsCount").GetInt32());
    }
    [Fact]
    public async Task InvalidPcaReturnsBadRequestNotOk()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/analysis", new { data = new[] { new[] { 1d } } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task MalformedJsonIsBadRequest()
    {
        var response = await _client.PostAsync("/api/v1/analysis", new StringContent("{bad}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task PreprocessingAndTransposeRoutesWork()
    {
        var transpose = await _client.PostAsJsonAsync("/api/v1/matrices/transpose", new { data = new[] { new[] { 1d, 2 }, new[] { 3d, 4 } } });
        Assert.Equal(HttpStatusCode.OK, transpose.StatusCode);
        var json = await transpose.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal(3, json.GetProperty("data")[0][1].GetDouble());
        var response = await _client.PostAsJsonAsync("/api/v1/preprocessing", new
        { data = new { datax = new[] { 1, 2 }, datay = new[] { 3, 4 } }, @params = new { prepX = new[] { "scaling" } } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    [Fact]
    public async Task RemovedTestRoutesAreGone()
    {
        foreach (var route in new[] { "/TestMatrix/TransposeMatrix", "/matrices/TransposeMatrix", "/TestPreprocess/process",
            "/minio/TestUploadFile", "/analysis/PCA", "/analysis/Analyze", "/minio/upload-file" })
            Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync(route, new StringContent(""))).StatusCode);
    }
    [Fact]
    public async Task MissingChunkReturnsBadRequest()
    {
        using var body = new MultipartFormDataContent
        {
            { new StringContent("data.bin"), "fileName" }, { new StringContent("1"), "totalChunks" }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsync("/api/v1/users/test-user/uploads/u1/chunks/0", body)).StatusCode);
    }
    [Fact]
    public async Task ExcelAnalysisRequiresExactlyOneSource()
    {
        using var none = new MultipartFormDataContent { { new StringContent("x"), "method" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync("/api/v1/analysis/excel", none)).StatusCode);
        using var both = new MultipartFormDataContent
        {
            { new ByteArrayContent(new byte[] { 1 }), "file", "a.xlsx" }, { new StringContent("b"), "bucket" }, { new StringContent("o.xlsx"), "objectName" }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync("/api/v1/analysis/excel", both)).StatusCode);
    }
    [Fact]
    public async Task TablesRequireCompleteRange()
    {
        var response = await _client.GetAsync("/api/v1/storage/b/tables?objectNames=a.xlsx&x1=1");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Все ошибки API — в формате ProblemDetails (RFC 7807).
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("x1, x2, y1 and y2", problem.RootElement.GetProperty("detail").GetString());
    }
    [Fact]
    public async Task SwaggerListsOnlyStandardizedEndpointsWithDescriptions()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, text);
        using var doc = JsonDocument.Parse(text);
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().ToList();
        var expected = new[]
        {
            "/api/v1/auth/register", "/api/v1/auth/login", "/api/v1/auth/logout", "/api/v1/auth/refresh",
            "/api/v1/roles", "/api/v1/roles/{roleName}",
            "/api/v1/users/by-username/{username}", "/api/v1/users/{userId}/roles", "/api/v1/users/{userId}/roles/{roleName}",
            "/api/v1/sessions/statuses", "/api/v1/sessions/statuses/latest",
            "/api/v1/users/{userId}/documents", "/api/v1/documents/{documentId}", "/api/v1/documents/{documentId}/versions",
            "/api/v1/document-versions/{versionId}", "/api/v1/document-versions/{versionId}/content",
            "/api/v1/users/{userId}/uploads/{uploadId}/chunks/{chunkIndex}", "/api/v1/users/{userId}/uploads/{uploadId}/complete",
            "/api/v1/storage/buckets/{bucket}", "/api/v1/storage/{bucket}/objects/{objectName}",
            "/api/v1/storage/{bucket}/presigned-urls/{objectName}", "/api/v1/storage/{bucket}/tables",
            "/api/v1/analysis/methods", "/api/v1/analysis", "/api/v1/analysis/excel",
            "/api/v1/matrices/transpose", "/api/v1/preprocessing"
        };
        Assert.Equal(expected.OrderBy(p => p), paths.Select(p => p.Name).OrderBy(p => p));
        int operations = 0;
        foreach (var path in paths)
        {
            Assert.StartsWith("/api/v1/", path.Name);
            Assert.DoesNotMatch("[A-Z_]", path.Name.Replace("{userId}", "").Replace("{roleName}", "").Replace("{documentId}", "")
                .Replace("{versionId}", "").Replace("{uploadId}", "").Replace("{chunkIndex}", "").Replace("{objectName}", ""));
            foreach (var operation in path.Value.EnumerateObject())
            {
                operations++;
                Assert.True(operation.Value.TryGetProperty("summary", out var summary) && summary.GetString()!.Length > 0,
                    $"{operation.Name.ToUpperInvariant()} {path.Name} has no description");
            }
        }
        Assert.Equal(34, operations);
    }
}
