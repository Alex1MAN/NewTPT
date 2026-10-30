using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using Minio;
using Minio.DataModel.Args;
using Npgsql;
using Xunit;
namespace JWTAuthTemplate.Tests;

// Только отдельный тестовый стек! Создаёт тестовых пользователей, документы и бакеты.
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEWTPT_TEST_URL")))
            Skip = "Set NEWTPT_TEST_URL for a dedicated PostgreSQL/MinIO/API test stack.";
    }
}
[Trait("Category", "Integration")]
public class DockerIntegrationTests
{
    private readonly HttpClient _client = new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("NEWTPT_TEST_URL") ?? "http://localhost:5505"),
        Timeout = TimeSpan.FromMinutes(3)
    };
    private static string Connection => Environment.GetEnvironmentVariable("NEWTPT_TEST_DB") ??
        "Host=localhost;Database=JWTAuthTemplate;Username=postgres;Password=root";
    private static IMinioClient Minio() => new MinioClient()
        .WithEndpoint(Environment.GetEnvironmentVariable("NEWTPT_TEST_MINIO") ?? "localhost:9000")
        .WithCredentials(Environment.GetEnvironmentVariable("NEWTPT_TEST_MINIO_USER") ?? "minioadmin",
            Environment.GetEnvironmentVariable("NEWTPT_TEST_MINIO_PASSWORD") ?? "minioadmin").Build();
    private static async Task<JsonElement> Ok(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {content}");
        var json = JsonDocument.Parse(content).RootElement.Clone();
        Assert.True(json.ValueKind == JsonValueKind.True || json.GetProperty("success").GetBoolean(), content);
        return json;
    }
    private async Task<(string id, string username, string token, string refresh)> Register()
    {
        var name = "test" + Guid.NewGuid().ToString("N");
        await Ok(await _client.PostAsJsonAsync("/Auth/Register", new { username = name, email = name + "@example.test",
            password = "TestPassword123!", confirmPassword = "TestPassword123!" }));
        var result = await Ok(await _client.PostAsJsonAsync("/Auth/Login", new { username = name, password = "TestPassword123!" }));
        var data = result.GetProperty("data");
        return (data.GetProperty("user").GetProperty("id").GetString()!, name,
            data.GetProperty("token").GetString()!, data.GetProperty("refreshToken").GetString()!);
    }
    private async Task<HttpResponseMessage> Upload(string bucket, string upload, int index, int total, byte[] bytes, string name = "data.bin")
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(bucket), "bucketName" }, { new StringContent(upload), "uploadId" },
            { new StringContent(name), "fileName" }, { new StringContent(index.ToString()), "chunkIndex" },
            { new StringContent(total.ToString()), "totalChunks" }, { new ByteArrayContent(bytes), "chunk", "part.bin" }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/minio/UploadChunk") { Content = form };
        request.Headers.ExpectContinue = true;
        return await _client.SendAsync(request);
    }
    private Task<HttpResponseMessage> Complete(string bucket, string upload, int count = 2, string name = "data.bin")
        => _client.PostAsJsonAsync("/minio/CompleteUpload", new { bucketName = bucket, uploadId = upload, fileName = name, totalChunks = count });
    [DockerFact]
    public async Task RealStorageParallelChunksAndParallelCompletionAreIdempotent()
    {
        var (id, _, _, _) = await Register();
        var bytes = new byte[11 * 1024 * 1024 + 31]; RandomNumberGenerator.Fill(bytes);
        // >5 MiB проверяет multipart-внутренности MinIO SDK и последовательный Stream.
        var first = bytes[..(6 * 1024 * 1024)];
        var second = bytes[(6 * 1024 * 1024)..];
        foreach (var response in await Task.WhenAll(Upload(id, "parallel", 1, 2, second), Upload(id, "parallel", 0, 2, first)))
            await Ok(response);
        await Ok(await Upload(id, "parallel", 0, 2, first));
        var completions = await Task.WhenAll(Complete(id, "parallel"), Complete(id, "parallel"));
        var a = await Ok(completions[0]); var b = await Ok(completions[1]);
        Assert.Equal(a.GetProperty("documentId").GetInt32(), b.GetProperty("documentId").GetInt32());
        var objectName = a.GetProperty("minioObjectName").GetString()!;
        var download = await _client.PostAsync("/minio/get-file?bucketName=" + id + "&objectName=" + Uri.EscapeDataString(objectName), null);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("true", download.Headers.GetValues("X-Success").Single());
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(await download.Content.ReadAsByteArrayAsync()));
        await using var db = new NpgsqlConnection(Connection); await db.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM \"Documents\" WHERE \"UserId\"=@id", db);
        cmd.Parameters.AddWithValue("id", id);
        Assert.Equal(1L, await cmd.ExecuteScalarAsync());
        cmd.CommandText = "SELECT count(*) FROM \"DocumentVersions\" v JOIN \"Documents\" d ON v.\"DocumentId\"=d.\"Id\" WHERE d.\"UserId\"=@id";
        Assert.Equal(1L, await cmd.ExecuteScalarAsync());
        cmd.CommandText = "SELECT count(*) FROM \"UserReferencesInMinio\" WHERE \"UserId\"=@id";
        Assert.Equal(1L, await cmd.ExecuteScalarAsync());
        using var minio = Minio();
        await Assert.ThrowsAnyAsync<Exception>(async () => await minio.StatObjectAsync(new StatObjectArgs().WithBucket(id)
            .WithObject("_chunks/" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id + "\nparallel"))).ToLowerInvariant() + "/0")));
    }
    [DockerFact]
    public async Task MissingChunkConflictingChunkAndUnknownSessionReturnNonOk()
    {
        var (id, _, _, _) = await Register();
        Assert.Equal(HttpStatusCode.NotFound, (await Complete(id, "unknown")).StatusCode);
        await Ok(await Upload(id, "partial", 0, 2, new byte[] { 1 }));
        Assert.Equal(HttpStatusCode.Conflict, (await Complete(id, "partial")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Upload(id, "partial", 0, 2, new byte[] { 2 })).StatusCode);
        await Ok(await Upload(id, "partial", 1, 2, new byte[] { 2 }));
        await Ok(await Complete(id, "partial"));
    }
    [DockerFact]
    public async Task BodyLimitIsEnforcedByRealKestrel()
    {
        var (id, _, _, _) = await Register();
        var response = await Upload(id, "oversize", 0, 1, new byte[9 * 1024 * 1024]);
        Assert.True(response.StatusCode is HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.BadRequest,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
    [DockerFact]
    public async Task LegacyRolesSessionsAndRefreshWorkAgainstPostgres()
    {
        var (id, username, token, refresh) = await Register();
        string role = "role" + Guid.NewGuid().ToString("N");
        await Ok(await _client.PostAsJsonAsync("/Auth/AddRole", new { name = role }));
        await Ok(await _client.PostAsJsonAsync("/User/AddRole?username=" + username, new { name = role }));
        var user = await Ok(await _client.GetAsync("/User/Get?username=" + username));
        Assert.Contains(role, user.GetRawText());
        using var remove = new HttpRequestMessage(HttpMethod.Delete, "/User/RemoveRole?username=" + username)
        { Content = JsonContent.Create(new { name = role }) };
        await Ok(await _client.SendAsync(remove));
        await Ok(await _client.PostAsJsonAsync("/User/SaveStatus?userId=" + id, new { fileName = "x", fileExtension = "xlsx", step = 1 }));
        await Ok(await _client.PostAsJsonAsync("/User/SaveStatus?userId=" + id, new { fileName = "x", fileExtension = "xlsx", step = 2 }));
        var statuses = await Ok(await _client.PostAsync("/User/GetAllStatusesByFileName?userId=" + id + "&fileName=x&fileExtension=xlsx", null));
        Assert.Equal(2, statuses.GetProperty("data").GetArrayLength());
        var first = await Ok(await _client.PostAsync("/User/GetLatestStatusByFileNameAndTime?userId=" + id +
            "&fileName=x&fileExtension=xlsx&timeFilter=" + Uri.EscapeDataString("01.01.2020 00:00:00"), null));
        Assert.Equal(1, first.GetProperty("data").GetProperty("statusParamsDict").GetProperty("step").GetInt32());
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        await Ok(await _client.PostAsJsonAsync("/sessions/save-status", new { fileName = "token-file", fileExtension = "csv" }));
        await Ok(await _client.GetAsync("/sessions/latest-status"));
        await Ok(await _client.PostAsJsonAsync("/Auth/RefreshToken", new { accessToken = token, refreshToken = refresh }));
    }
    [DockerFact]
    public async Task ExcelUploadLegacyReadPresignedUrlAndPcaAgree()
    {
        var (id, _, _, _) = await Register();
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Data");
        sheet.Cell(1, 1).Value = "10"; sheet.Cell(1, 2).Value = "20";
        for (int i = 1; i <= 3; i++) { sheet.Cell(i + 1, 1).Value = i; sheet.Cell(i + 1, 2).Value = 2 * i; }
        using var stream = new MemoryStream(); workbook.SaveAs(stream);
        using var form = new MultipartFormDataContent
        { { new StringContent(id), "bucketName" }, { new ByteArrayContent(stream.ToArray()), "filesData", "sample.xlsx" } };
        var uploaded = await Ok(await _client.PostAsync("/minio/UploadFilesUpdateReferences_2", form));
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync("/minio/UploadFilesUpdateReferences_2", form)).StatusCode);
        int docId = uploaded.GetProperty("data").GetProperty("references")[0].GetProperty("documentId").GetInt32();
        var versions = await Ok(await _client.GetAsync($"/minio/documents/{docId}/versions"));
        string objectName = versions.GetProperty("data")[0].GetProperty("minioObjectName").GetString()!;
        string query = "?bucketName=" + id + "&objectName=" + Uri.EscapeDataString(objectName);
        var urlResponse = await Ok(await _client.GetAsync("/minio/GetUrlFileFromMinio" + query));
        var url = urlResponse.GetProperty("data").GetProperty("url").GetString()!;
        Assert.Equal(stream.ToArray(), await _client.GetByteArrayAsync(url));
        var pca = await Ok(await _client.PostAsync("/analysis/AnalyzeStoredExcel" + query, null));
        Assert.Equal(1, pca.GetProperty("data").GetProperty("explainedVarianceRatio")[0].GetDouble(), 12);
        var content = await Ok(await _client.GetAsync("/minio/GetAllContentFromFiles?bucketName=" + id +
            "&fileNames=" + Uri.EscapeDataString(objectName) + "&allFilesExtension=xlsx&inputX1=0&inputX2=0&inputY1=0&inputY2=0"));
        using var table = JsonDocument.Parse(content.GetProperty("data").GetString()!);
        Assert.Equal(2, table.RootElement.GetProperty("labels").GetArrayLength());
        using var excel = new MultipartFormDataContent { { new ByteArrayContent(stream.ToArray()), "file", "sample.xlsx" } };
        await Ok(await _client.PostAsync("/analysis/AnalyzeExcel", excel));
    }
    [DockerFact]
    public async Task ExistingDocumentVersionEndpointsRemainOperational()
    {
        var (id, _, _, _) = await Register();
        using var form = new MultipartFormDataContent
        { { new StringContent(id), "bucketName" }, { new ByteArrayContent(new byte[] { 1, 2 }), "filesData", "versions.bin" } };
        var upload = await Ok(await _client.PostAsync("/minio/upload-files?bucketName=" + id, form));
        int docId = upload.GetProperty("data").GetProperty("references")[0].GetProperty("documentId").GetInt32();
        using var version = new MultipartFormDataContent { { new ByteArrayContent(new byte[] { 3, 4 }), "file", "versions.bin" } };
        var added = await Ok(await _client.PostAsync($"/minio/documents/{docId}/versions", version));
        int versionId = added.GetProperty("data").GetProperty("versionId").GetInt32();
        var versions = await Ok(await _client.GetAsync($"/minio/documents/{docId}/versions"));
        Assert.Equal(2, versions.GetProperty("data").GetArrayLength());
        await Ok(await _client.DeleteAsync($"/minio/documents/versions/{versionId}"));
        await Ok(await _client.DeleteAsync($"/minio/documents/{docId}"));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/minio/documents/detail/{docId}")).StatusCode);
    }
}
