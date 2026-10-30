using JWTAuthTemplate.Models.Identity;
namespace JWTAuthTemplate.Application.Interfaces;
public interface ILegacyUserService
{
    Task AddRole(string username, string roleName);
    Task RemoveRole(string username, string roleName);
    Task SaveStatus(string userId, Dictionary<string, object> statusParams);
    Task<UserSessionStatus?> GetLatestStatus(string userId);
    Task<List<UserSessionStatus>> GetAllStatusesByFileName(string userId, string fileName, string fileExtension);
    Task<UserSessionStatus?> GetLatestStatusByFileName(string userId, string fileName, string fileExtension);
    Task<UserSessionStatus?> GetLatestStatusByFileNameAndTime(string userId, string fileName, string fileExtension, string timeFilter);
}
