using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Статусы пользовательских сессий.</summary>
public interface ISessionService
{
    /// <summary>Сохраняет статус; из параметров извлекаются fileName и fileExtension для фильтрации.</summary>
    Task<int> SaveStatusAsync(string userId, Dictionary<string, object> statusParams);

    /// <summary>
    /// Последний статус пользователя: по всем файлам, по конкретному файлу (fileName + fileExtension)
    /// и, при необходимости, не позже момента <paramref name="asOfUtc"/>.
    /// </summary>
    Task<UserSessionStatusDTO?> GetLatestAsync(string userId, string? fileName = null, string? fileExtension = null, DateTime? asOfUtc = null);

    /// <summary>Все статусы файла, от новых к старым.</summary>
    Task<List<UserSessionStatusDTO>> GetByFileAsync(string userId, string fileName, string fileExtension);
}
