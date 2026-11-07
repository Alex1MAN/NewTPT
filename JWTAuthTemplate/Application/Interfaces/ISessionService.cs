using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Статусы пользовательских сессий (общая логика для /sessions и /User).</summary>
public interface ISessionService
{
    Task<int> SaveStatusAsync(string userId, Dictionary<string, object> statusParams);
    Task<UserSessionStatusDTO?> GetLatestAsync(string userId);
    Task<List<UserSessionStatusDTO>> GetAllByFileAsync(string userId, string fileName, string fileExtension);
    Task<UserSessionStatusDTO?> GetLatestByFileAsync(string userId, string fileName, string fileExtension);

    /// <summary>Последний статус файла не позже указанного момента (UTC).</summary>
    Task<UserSessionStatusDTO?> GetLatestByFileAsOfAsync(string userId, string fileName, string fileExtension, DateTime asOfUtc);

    /// <summary>Первый статус файла не раньше указанного момента (UTC) — семантика master-версии.</summary>
    Task<UserSessionStatusDTO?> GetFirstByFileSinceAsync(string userId, string fileName, string fileExtension, DateTime sinceUtc);
}
