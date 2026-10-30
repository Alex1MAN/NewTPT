using JWTAuthTemplate.Models.Identity;
namespace JWTAuthTemplate.Application.Interfaces;
public interface IReferencesRepository
{
    Task AddReferenceInPostgre(UserReferencesInMinio reference);
    Task<List<UserReferencesInMinio>> GetFileReferencesByUserId(string userId);
}
