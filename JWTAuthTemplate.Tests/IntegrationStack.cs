using System.Net.Sockets;
using Npgsql;
using Xunit;

namespace JWTAuthTemplate.Tests;

/// <summary>
/// Параметры тестового стека (API + PostgreSQL + MinIO). По умолчанию — адреса из docker-compose.yml;
/// переопределяются переменными NEWTPT_TEST_URL, NEWTPT_TEST_DB, NEWTPT_TEST_MINIO, NEWTPT_TEST_MINIO_USER, NEWTPT_TEST_MINIO_PASSWORD.
/// Интеграционные тесты выполняются автоматически, если API и БД доступны, иначе пропускаются с причиной.
/// </summary>
public static class IntegrationStack
{
    public static readonly Uri ApiUrl = new(Environment.GetEnvironmentVariable("NEWTPT_TEST_URL") ?? "http://localhost:5505");

    public static readonly string Connection = Environment.GetEnvironmentVariable("NEWTPT_TEST_DB") ??
        "Host=localhost;Database=JWTAuthTemplate;Username=postgres;Password=root";

    public static readonly string MinioEndpoint = Environment.GetEnvironmentVariable("NEWTPT_TEST_MINIO") ?? "localhost:9000";
    public static readonly string MinioUser = Environment.GetEnvironmentVariable("NEWTPT_TEST_MINIO_USER") ?? "minioadmin";
    public static readonly string MinioPassword = Environment.GetEnvironmentVariable("NEWTPT_TEST_MINIO_PASSWORD") ?? "minioadmin";

    private static readonly Lazy<string?> SkipReason = new(Probe);

    /// <summary>null, если стек доступен; иначе причина пропуска.</summary>
    public static string? Unavailable => SkipReason.Value;

    private static string? Probe()
    {
        if (Environment.GetEnvironmentVariable("NEWTPT_SKIP_INTEGRATION") == "1")
            return "NEWTPT_SKIP_INTEGRATION=1: интеграционные тесты отключены.";
        var problems = new List<string>();
        if (!TcpOpen(ApiUrl.Host, ApiUrl.Port))
            problems.Add($"API {ApiUrl} недоступен");
        try
        {
            using var db = new NpgsqlConnection(Connection);
            db.Open();
        }
        catch (Exception ex)
        {
            problems.Add("PostgreSQL недоступен: " + ex.Message);
        }
        var minio = MinioEndpoint.Split(':');
        if (!TcpOpen(minio[0], minio.Length > 1 ? int.Parse(minio[1]) : 9000))
            problems.Add($"MinIO {MinioEndpoint} недоступен");
        return problems.Count == 0
            ? null
            : string.Join("; ", problems) + ". Запустите стек: docker compose up -d --build (или задайте NEWTPT_TEST_*).";
    }

    private static bool TcpOpen(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync(host, port).Wait(TimeSpan.FromSeconds(2)) && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Факт, выполняемый только при доступном тестовом стеке.</summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute() => Skip = IntegrationStack.Unavailable;
}
