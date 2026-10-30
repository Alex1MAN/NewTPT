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
        var response = await _client.PostAsJsonAsync("/analysis/PCA", new { data = new[] { new[] { 1d }, new[] { 2d } } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal(1, json.GetProperty("data").GetProperty("componentsCount").GetInt32());
    }
    [Fact]
    public async Task InvalidPcaReturnsBadRequestNotOk()
    {
        var response = await _client.PostAsJsonAsync("/analysis/PCA", new { data = new[] { new[] { 1d } } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task MalformedJsonIsBadRequest()
    {
        var response = await _client.PostAsync("/analysis/PCA", new StringContent("{bad}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task PreprocessAndTransposeRoutesPreserved()
    {
        var transpose = await _client.PostAsJsonAsync("/TestMatrix/TransposeMatrix", new { data = new[] { new[] { 1d, 2 }, new[] { 3d, 4 } } });
        Assert.Equal(HttpStatusCode.OK, transpose.StatusCode);
        Assert.True((await transpose.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean());
        var response = await _client.PostAsJsonAsync("/TestPreprocess/process", new
        { data = new { datax = new[] { 1, 2 }, datay = new[] { 3, 4 } }, @params = new { prepX = new[] { "scaling" } } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    [Fact]
    public async Task MissingChunkReturnsBadRequest()
    {
        using var body = new MultipartFormDataContent
        {
            { new StringContent("test-user"), "bucketName" }, { new StringContent("u1"), "uploadId" },
            { new StringContent("data.bin"), "fileName" }, { new StringContent("0"), "chunkIndex" },
            { new StringContent("1"), "totalChunks" }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync("/minio/UploadChunk", body)).StatusCode);
    }
    [Fact]
    public async Task SwaggerContainsEveryMasterEndpoint()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, text);
        using var doc = JsonDocument.Parse(text);
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name.ToLowerInvariant()).ToHashSet();
        foreach (var method in new[] { "UploadChunk", "CompleteUpload", "CreateBucket", "UploadFilesUpdateReferences",
            "UploadFilesUpdateReferences_2", "TestUploadFile", "AddReferenceInPostgre", "GetFileReferencesByUserId",
            "GetUrlFileFromMinio", "GetFileContent", "GetAllTableFromExcel", "GetTableFromExcelWithLimits", "GetTablesFromSeveralSPC", "GetAllContentFromFiles" })
            Assert.Contains("/minio/" + method.ToLowerInvariant(), paths);
        foreach (var method in new[] { "Get", "AddRole", "RemoveRole", "SaveStatus", "GetLatestStatus",
            "GetAllStatusesByFileName", "GetLatestStatusByFileName", "GetLatestStatusByFileNameAndTime" })
            Assert.Contains("/user/" + method.ToLowerInvariant(), paths);
        foreach (var method in new[] { "Register", "Login", "Logout", "AddRole", "RefreshToken", "GetRoles" })
            Assert.Contains("/auth/" + method.ToLowerInvariant(), paths);
    }
}
