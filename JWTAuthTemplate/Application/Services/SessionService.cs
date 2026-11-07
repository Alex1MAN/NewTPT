using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Services;

/// <summary>
/// Статусы пользовательских сессий. Один сервис обслуживает и /sessions (пользователь из токена),
/// и legacy-маршруты /User (пользователь из параметра userId).
/// </summary>
public sealed class SessionService : ISessionService
{
    private readonly ISessionStatusRepository _repository;

    public SessionService(ISessionStatusRepository repository) => _repository = repository;

    public async Task<int> SaveStatusAsync(string userId, Dictionary<string, object> statusParams)
    {
        if (statusParams == null || statusParams.Count == 0)
            throw new ArgumentException("Status parameters cannot be empty.");
        await EnsureUserAsync(userId);
        return await _repository.AddAsync(new UserSessionStatus
        {
            UserId = userId,
            ActualAt = DateTime.UtcNow,
            StatusParamsDict = statusParams,
            FileName = ValueOf(statusParams, "fileName"),
            FileExtension = ValueOf(statusParams, "fileExtension")
        });
    }

    public async Task<UserSessionStatusDTO?> GetLatestAsync(string userId)
        => ToDto(await _repository.GetLatestAsync(userId));

    public async Task<List<UserSessionStatusDTO>> GetAllByFileAsync(string userId, string fileName, string fileExtension)
    {
        ValidateFile(fileName, fileExtension);
        return (await _repository.GetByFileAsync(userId, fileName, fileExtension)).Select(s => ToDto(s)!).ToList();
    }

    public async Task<UserSessionStatusDTO?> GetLatestByFileAsync(string userId, string fileName, string fileExtension)
    {
        ValidateFile(fileName, fileExtension);
        return ToDto(await _repository.GetLatestAsync(userId, fileName, fileExtension));
    }

    public async Task<UserSessionStatusDTO?> GetLatestByFileAsOfAsync(string userId, string fileName, string fileExtension, DateTime asOfUtc)
    {
        ValidateFile(fileName, fileExtension);
        return ToDto(await _repository.GetLatestAsync(userId, fileName, fileExtension, asOfUtc));
    }

    public async Task<UserSessionStatusDTO?> GetFirstByFileSinceAsync(string userId, string fileName, string fileExtension, DateTime sinceUtc)
    {
        ValidateFile(fileName, fileExtension);
        return ToDto(await _repository.GetFirstSinceAsync(userId, fileName, fileExtension, sinceUtc));
    }

    private async Task EnsureUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || !await _repository.UserExistsAsync(userId))
            throw new KeyNotFoundException("User not found.");
    }

    private static void ValidateFile(string fileName, string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Parameter 'fileName' is required");
        if (string.IsNullOrWhiteSpace(fileExtension))
            throw new ArgumentException("Parameter 'fileExtension' is required");
    }

    /// <summary>Значение ключа: сначала точное совпадение, затем без учёта регистра (как в SQL-скрипте восстановления).</summary>
    private static string ValueOf(Dictionary<string, object> values, string key)
    {
        if (values.TryGetValue(key, out var exact))
            return exact?.ToString() ?? "";
        var match = values.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        return match.Value?.ToString() ?? "";
    }

    private static UserSessionStatusDTO? ToDto(UserSessionStatus? s) => s == null ? null : new UserSessionStatusDTO
    {
        Id = s.Id,
        UserId = s.UserId,
        ActualAt = s.ActualAt,
        StatusParams = s.StatusParams,
        StatusParamsDict = s.StatusParamsDict,
        FileName = s.FileName,
        FileExtension = s.FileExtension
    };
}
