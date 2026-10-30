using JWTAuthTemplate.Models.Identity;
namespace JWTAuthTemplate.Application.Interfaces;
public interface IFileContentService
{
    Task EnsureUniqueFiles(string bucketName, IReadOnlyList<string> fileNames);
    Task AddReferenceInPostgre(UserReferencesInMinio reference);
    Task<List<UserReferencesInMinio>> GetFileReferencesByUserId(string userId);
    Task<string> GetTablesFromSeveralSPC(string bucketName, string[] fileNames);
    Task<string> GetAllContentFromFiles(string bucketName, string[] fileNames, string allFilesExtension,
        double inputX1, double inputX2, double inputY1, double inputY2);
}
