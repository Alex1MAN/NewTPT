using System.Globalization;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Models.Identity;
namespace JWTAuthTemplate.Application.Services;
public sealed class LegacyUserService : ILegacyUserService
{
    private readonly ILegacyUserRepository _repository;
    public LegacyUserService(ILegacyUserRepository repository) => _repository = repository;
    public Task AddRole(string username, string roleName) => _repository.AddRole(username, roleName);
    public Task RemoveRole(string username, string roleName) => _repository.RemoveRole(username, roleName);
    public Task SaveStatus(string userId, Dictionary<string, object> statusParams)
    {
        if (statusParams == null || statusParams.Count == 0) throw new ArgumentException("Status parameters cannot be empty.");
        return _repository.SaveStatus(userId, statusParams);
    }
    public async Task<UserSessionStatus?> GetLatestStatus(string userId) => (await _repository.GetStatuses(userId)).FirstOrDefault();
    public async Task<List<UserSessionStatus>> GetAllStatusesByFileName(string userId, string fileName, string fileExtension)
        => (await _repository.GetStatuses(userId)).Where(s =>
            s.StatusParamsDict.GetValueOrDefault("fileName")?.ToString() == fileName &&
            s.StatusParamsDict.GetValueOrDefault("fileExtension")?.ToString() == fileExtension).ToList();
    public async Task<UserSessionStatus?> GetLatestStatusByFileName(string userId, string fileName, string fileExtension)
        => (await GetAllStatusesByFileName(userId, fileName, fileExtension)).FirstOrDefault();
    public async Task<UserSessionStatus?> GetLatestStatusByFileNameAndTime(string userId, string fileName, string fileExtension, string timeFilter)
    {
        if (!DateTime.TryParseExact(timeFilter, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var filter))
            throw new ArgumentException("Wrong date format: use dd.MM.yyyy HH:mm:ss (UTC).");
        // Сохраняем семантику master: первый статус ПОСЛЕ указанного времени.
        return (await GetAllStatusesByFileName(userId, fileName, fileExtension))
            .Where(s => s.ActualAt >= filter).OrderBy(s => s.ActualAt).ThenBy(s => s.Id).FirstOrDefault();
    }
}
