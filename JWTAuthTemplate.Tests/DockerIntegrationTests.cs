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

/// <summary>
/// Сквозные проверки через реальные API, PostgreSQL и MinIO (см. IntegrationStack). Создают тестовых
/// пользователей, документы и бакеты, поэтому выполняются только против тестового стека.
/// </summary>
[Trait("Category", "Integration")]
public class DockerIntegrationTests
{
    private readonly HttpClient _client = new() { BaseAddress = IntegrationStack.ApiUrl, Timeout = TimeSpan.FromMinutes(3) };
    private static IMinioClient Minio() => new MinioClient()
        .WithEndpoint(IntegrationStack.MinioEndpoint)
        .WithCredentials(IntegrationStack.MinioUser, IntegrationStack.MinioPassword).Build();
    private static async Task<JsonElement> Ok(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {content}");
        if (response.StatusCode == HttpStatusCode.NoContent) return default;
        var json = JsonDocument.Parse(content).RootElement.Clone();
        Assert.True(json.ValueKind == JsonValueKind.True || json.GetProperty("success").GetBoolean(), content);
        return json;
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response) => (await Ok(response)).GetProperty("data");
    private async Task<(string id, string username, string token, string refresh)> Register()
    {
        var name = "test" + Guid.NewGuid().ToString("N");
        await Ok(await _client.PostAsJsonAsync("/api/v1/auth/register", new { username = name, email = name + "@example.test",
            password = "TestPassword123!", confirmPassword = "TestPassword123!" }));
        var data = await Data(await _client.PostAsJsonAsync("/api/v1/auth/login", new { username = name, password = "TestPassword123!" }));
        return (data.GetProperty("user").GetProperty("id").GetString()!, name,
            data.GetProperty("token").GetString()!, data.GetProperty("refreshToken").GetString()!);
    }
    private async Task<HttpResponseMessage> Upload(string user, string upload, int index, int total, byte[] bytes, string name = "data.bin")
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(name), "fileName" }, { new StringContent(total.ToString()), "totalChunks" },
            { new ByteArrayContent(bytes), "chunk", "part.bin" }
        };
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/users/{user}/uploads/{upload}/chunks/{index}") { Content = form };
        request.Headers.ExpectContinue = true;
        return await _client.SendAsync(request);
    }
    private Task<HttpResponseMessage> Complete(string user, string upload, int count = 2, string name = "data.bin")
        => _client.PostAsJsonAsync($"/api/v1/users/{user}/uploads/{upload}/complete", new { fileName = name, totalChunks = count });
    private static MultipartFormDataContent Files(params (string name, byte[] bytes)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, bytes) in files) form.Add(new ByteArrayContent(bytes), "files", name);
        return form;
    }
    private static byte[] Workbook()
    {
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Data");
        sheet.Cell(1, 1).Value = "10"; sheet.Cell(1, 2).Value = "20";
        for (int i = 1; i <= 3; i++) { sheet.Cell(i + 1, 1).Value = i; sheet.Cell(i + 1, 2).Value = 2 * i; }
        using var stream = new MemoryStream(); workbook.SaveAs(stream);
        return stream.ToArray();
    }

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
        var a = await Data(completions[0]); var b = await Data(completions[1]);
        Assert.Equal(a.GetProperty("documentId").GetInt32(), b.GetProperty("documentId").GetInt32());
        var objectName = a.GetProperty("minioObjectName").GetString()!;
        var download = await _client.GetAsync($"/api/v1/storage/{id}/objects/{objectName}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("true", download.Headers.GetValues("X-Success").Single());
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(await download.Content.ReadAsByteArrayAsync()));
        await using var db = new NpgsqlConnection(IntegrationStack.Connection); await db.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM \"Documents\" WHERE \"UserId\"=@id", db);
        cmd.Parameters.AddWithValue("id", id);
        Assert.Equal(1L, await cmd.ExecuteScalarAsync());
        cmd.CommandText = "SELECT count(*) FROM \"DocumentVersions\" v JOIN \"Documents\" d ON v.\"DocumentId\"=d.\"Id\" WHERE d.\"UserId\"=@id";
        Assert.Equal(1L, await cmd.ExecuteScalarAsync());
        // Историческая таблица master-версии больше не заполняется.
        cmd.CommandText = "SELECT count(*) FROM \"UserReferencesInMinio\" WHERE \"UserId\"=@id";
        Assert.Equal(0L, await cmd.ExecuteScalarAsync());
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
    public async Task RolesUsersSessionsAndRefreshWorkAgainstPostgres()
    {
        var (id, username, token, refresh) = await Register();
        string role = "role" + Guid.NewGuid().ToString("N");
        var created = await _client.PostAsJsonAsync("/api/v1/roles", new { name = role });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(role, (await Data(created)).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("/api/v1/roles", new { name = role })).StatusCode);
        Assert.Contains(role, (await Data(await _client.GetAsync("/api/v1/roles"))).EnumerateArray().Select(r => r.GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsync($"/api/v1/users/{id}/roles/{role}", null)).StatusCode);
        var repeat = await _client.PutAsync($"/api/v1/users/{id}/roles/{role}", null);
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        using var problem = JsonDocument.Parse(await repeat.Content.ReadAsStringAsync());
        Assert.Equal(409, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal($"User already in role '{role}'.", problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsync($"/api/v1/users/{id}/roles/no-such-role", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsync($"/api/v1/users/no-such-user/roles/{role}", null)).StatusCode);

        var user = await Data(await _client.GetAsync("/api/v1/users/by-username/" + username));
        Assert.Equal(id, user.GetProperty("id").GetString());
        Assert.Contains(role, user.GetRawText());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/users/by-username/nobody" + Guid.NewGuid().ToString("N"))).StatusCode);
        Assert.Equal(new[] { role }, (await Data(await _client.GetAsync($"/api/v1/users/{id}/roles"))).EnumerateArray().Select(r => r.GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v1/users/{id}/roles/{role}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/v1/users/{id}/roles/{role}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v1/roles/{role}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/v1/roles/{role}")).StatusCode);

        // Статусы сессий: пользователь берётся из токена.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/sessions/statuses/latest")).StatusCode);
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/sessions/statuses/latest")).StatusCode);
        var saved = await _client.PostAsJsonAsync("/api/v1/sessions/statuses", new { fileName = "x", fileExtension = "xlsx", step = 1 });
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        Assert.True((await Data(saved)).GetProperty("id").GetInt32() > 0);
        await Ok(await _client.PostAsJsonAsync("/api/v1/sessions/statuses", new { fileName = "x", fileExtension = "xlsx", step = 2 }));
        await Ok(await _client.PostAsJsonAsync("/api/v1/sessions/statuses", new { fileName = "other", fileExtension = "csv", step = 3 }));

        var statuses = await Data(await _client.GetAsync("/api/v1/sessions/statuses?fileName=x&fileExtension=xlsx"));
        Assert.Equal(new[] { 2, 1 }, statuses.EnumerateArray().Select(s => s.GetProperty("statusParamsDict").GetProperty("step").GetInt32()));
        Assert.Empty((await Data(await _client.GetAsync("/api/v1/sessions/statuses?fileName=none&fileExtension=xlsx"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/v1/sessions/statuses?fileName=x")).StatusCode);

        var latest = await Data(await _client.GetAsync("/api/v1/sessions/statuses/latest"));
        Assert.Equal("other", latest.GetProperty("fileName").GetString());
        Assert.False(latest.TryGetProperty("statusParams", out _));
        var latestFile = await Data(await _client.GetAsync("/api/v1/sessions/statuses/latest?fileName=x&fileExtension=xlsx"));
        Assert.Equal(2, latestFile.GetProperty("statusParamsDict").GetProperty("step").GetInt32());
        var asOf = Uri.EscapeDataString(statuses[1].GetProperty("actualAt").GetDateTime().ToString("O"));
        var atFirst = await Data(await _client.GetAsync($"/api/v1/sessions/statuses/latest?fileName=x&fileExtension=xlsx&asOf={asOf}"));
        Assert.Equal(1, atFirst.GetProperty("statusParamsDict").GetProperty("step").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/v1/sessions/statuses/latest?asOf=2026-01-01T00:00:00Z")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/v1/sessions/statuses/latest?fileName=x&fileExtension=xlsx&asOf=bad")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/sessions/statuses/latest?fileName=x&fileExtension=xlsx&asOf=2000-01-01T00:00:00Z")).StatusCode);

        var refreshed = await Data(await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { accessToken = token, refreshToken = refresh }));
        Assert.Equal(id, refreshed.GetProperty("user").GetProperty("id").GetString());
        await Ok(await _client.PostAsync("/api/v1/auth/logout", null));
    }
    [DockerFact]
    public async Task ExcelUploadTablesPresignedUrlAndPcaAgree()
    {
        var (id, _, _, _) = await Register();
        var excel = Workbook();
        var uploaded = await Data(await _client.PostAsync($"/api/v1/users/{id}/documents", Files(("sample.xlsx", excel))));
        Assert.Equal(1, uploaded.GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/v1/users/{id}/documents", Files(("sample.xlsx", excel)))).StatusCode);
        var duplicate = await Data(await _client.PostAsync($"/api/v1/users/{id}/documents?allowDuplicateNames=true", Files(("sample.xlsx", excel))));
        Assert.Equal(1, duplicate.GetProperty("count").GetInt32());
        Assert.Equal(2, (await Data(await _client.GetAsync($"/api/v1/users/{id}/documents"))).GetArrayLength());

        string objectName = uploaded.GetProperty("references")[0].GetProperty("minioObjectName").GetString()!;
        var url = (await Data(await _client.GetAsync($"/api/v1/storage/{id}/presigned-urls/{objectName}"))).GetProperty("url").GetString()!;
        Assert.Equal(excel, await _client.GetByteArrayAsync(url));
        Assert.Equal(excel, await _client.GetByteArrayAsync($"/api/v1/storage/{id}/objects/{objectName}"));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/storage/{id}/objects/missing/file.xlsx")).StatusCode);

        // Анализ: загруженный файл и объект хранилища дают одинаковый результат через один эндпоинт.
        using var fromStorage = new MultipartFormDataContent { { new StringContent(id), "bucket" }, { new StringContent(objectName), "objectName" } };
        var pca = await Data(await _client.PostAsync("/api/v1/analysis/excel", fromStorage));
        Assert.Equal(1, pca.GetProperty("explainedVarianceRatio")[0].GetDouble(), 12);
        using var fromFile = new MultipartFormDataContent { { new ByteArrayContent(excel), "file", "sample.xlsx" } };
        var pcaFile = await Data(await _client.PostAsync("/api/v1/analysis/excel", fromFile));
        Assert.Equal(pca.GetProperty("scores").GetRawText(), pcaFile.GetProperty("scores").GetRawText());

        // Таблицы: без диапазона и с диапазоном (остаётся столбец "10", значения ограничены [0; 2]).
        var tables = await Data(await _client.GetAsync($"/api/v1/storage/{id}/tables?objectNames=" + Uri.EscapeDataString(objectName)));
        Assert.Equal(1, tables.GetArrayLength());
        Assert.Equal("xlsx", tables[0].GetProperty("format").GetString());
        using (var table = JsonDocument.Parse(tables[0].GetProperty("table").GetString()!))
            Assert.Equal(2, table.RootElement.GetProperty("labels").GetArrayLength());
        var limited = await Data(await _client.GetAsync($"/api/v1/storage/{id}/tables?objectNames=" + Uri.EscapeDataString(objectName) + "&x1=15&x2=5&y1=0&y2=2"));
        using (var table = JsonDocument.Parse(limited[0].GetProperty("table").GetString()!))
        {
            Assert.Equal(new[] { "10" }, table.RootElement.GetProperty("labels").EnumerateArray().Select(l => l.GetString()));
            Assert.Equal("2", table.RootElement.GetProperty("values")[0][2].GetProperty("10").GetString());
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/v1/storage/{id}/tables?objectNames=a.txt")).StatusCode);
    }
    [DockerFact]
    public async Task DocumentVersionEndpointsRemainOperational()
    {
        var (id, _, _, _) = await Register();
        var upload = await Data(await _client.PostAsync($"/api/v1/users/{id}/documents", Files(("versions.bin", new byte[] { 1, 2 }), ("second.bin", new byte[] { 9 }))));
        Assert.Equal(2, upload.GetProperty("count").GetInt32());
        int docId = upload.GetProperty("references")[0].GetProperty("documentId").GetInt32();
        using var version = new MultipartFormDataContent { { new ByteArrayContent(new byte[] { 3, 4 }), "file", "versions.bin" } };
        var added = await Data(await _client.PostAsync($"/api/v1/documents/{docId}/versions", version));
        int versionId = added.GetProperty("versionId").GetInt32();
        Assert.Equal(new byte[] { 3, 4 }, await _client.GetByteArrayAsync($"/api/v1/document-versions/{versionId}/content"));
        Assert.Equal(2, (await Data(await _client.GetAsync($"/api/v1/documents/{docId}/versions"))).GetArrayLength());
        Assert.Equal(2, (await Data(await _client.GetAsync($"/api/v1/documents/{docId}"))).GetProperty("currentVersionNumber").GetInt32());
        Assert.Equal(versionId, (await Data(await _client.GetAsync($"/api/v1/document-versions/{versionId}"))).GetProperty("versionId").GetInt32());
        using var wrong = new MultipartFormDataContent { { new ByteArrayContent(new byte[] { 3 }), "file", "versions.txt" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync($"/api/v1/documents/{docId}/versions", wrong)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v1/document-versions/{versionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/v1/document-versions/{versionId}")).StatusCode);
        int lastVersion = (await Data(await _client.GetAsync($"/api/v1/documents/{docId}/versions")))[0].GetProperty("versionId").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"/api/v1/document-versions/{lastVersion}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v1/documents/{docId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/documents/{docId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/v1/documents/{docId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsync($"/api/v1/storage/buckets/{id}", null)).StatusCode);
    }
}
