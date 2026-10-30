using JWTAuthTemplate.Models.Identity;
namespace JWTAuthTemplate.Application.Interfaces;
public interface ILegacyUserRepository
{
    Task AddRole(string username, string roleName);
    Task RemoveRole(string username, string roleName);
    Task SaveStatus(string userId, Dictionary<string, object> statusParams);
    Task<List<UserSessionStatus>> GetStatuses(string userId);
}
