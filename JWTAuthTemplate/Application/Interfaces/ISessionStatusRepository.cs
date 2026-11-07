using JWTAuthTemplate.Application.Models;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Порт хранилища статусов пользовательских сессий.</summary>
public interface ISessionStatusRepository
{
    Task<bool> UserExistsAsync(string userId);
    Task<int> AddAsync(UserSessionStatus status);

    /// <summary>Последний статус пользователя (необязательно — не позже <paramref name="asOf"/>).</summary>
    Task<UserSessionStatus?> GetLatestAsync(string userId, string? fileName = null, string? fileExtension = null, DateTime? asOf = null);

    /// <summary>Первый статус файла не раньше <paramref name="since"/>.</summary>
    Task<UserSessionStatus?> GetFirstSinceAsync(string userId, string fileName, string fileExtension, DateTime since);

    /// <summary>Все статусы файла, от новых к старым.</summary>
    Task<List<UserSessionStatus>> GetByFileAsync(string userId, string fileName, string fileExtension);
}
