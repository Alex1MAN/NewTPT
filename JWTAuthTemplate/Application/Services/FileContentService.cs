using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Models.Identity;
namespace JWTAuthTemplate.Application.Services;
public sealed class FileContentService : IFileContentService
{
    private readonly IMinioService _minio;
    private readonly IReferencesRepository _references;
    public FileContentService(IMinioService minio, IReferencesRepository references) => (_minio, _references) = (minio, references);
    public async Task EnsureUniqueFiles(string bucketName, IReadOnlyList<string> fileNames)
    {
        var existing = await _minio.GetDocumentsByUserIdAsync(bucketName);
        var names = existing.Select(d => d.FileName).ToHashSet(StringComparer.Ordinal);
        if (fileNames.Any(n => !names.Add(n)))
            throw new UploadConflictException("File already exists. Rename the file or create a new version.");
    }
    public Task AddReferenceInPostgre(UserReferencesInMinio reference)
    {
        if (string.IsNullOrWhiteSpace(reference.UserId) || string.IsNullOrWhiteSpace(reference.FileName) ||
            string.IsNullOrWhiteSpace(reference.FileReferenceMinio))
            throw new ArgumentException("UserId, FileName and FileReferenceMinio are required.");
        reference.Id = 0;
        return _references.AddReferenceInPostgre(reference);
    }
    public Task<List<UserReferencesInMinio>> GetFileReferencesByUserId(string userId)
        => _references.GetFileReferencesByUserId(userId);
    public async Task<string> GetTablesFromSeveralSPC(string bucketName, string[] fileNames)
    {
        ValidateFiles(fileNames);
        var results = new List<string>();
        foreach (var name in fileNames) results.Add(await _minio.GetSPCFileContentAsJson(bucketName, name));
        return "[" + string.Join(",", results) + "]";
    }
    public Task<string> GetAllContentFromFiles(string bucketName, string[] fileNames, string allFilesExtension,
        double inputX1, double inputX2, double inputY1, double inputY2)
    {
        ValidateFiles(fileNames);
        if (new[] { inputX1, inputX2, inputY1, inputY2 }.Any(v => !double.IsFinite(v)))
            throw new ArgumentException("Bounds must be finite.");
        string extension = allFilesExtension?.TrimStart('.').ToLowerInvariant() ?? "";
        return extension switch
        {
            "xls" or "xlsx" when inputX1 == inputX2 && inputX2 == inputY1 && inputY1 == inputY2
                => _minio.GetExcelFileContentAsJson(bucketName, fileNames[0]),
            "xls" or "xlsx" => _minio.GetExcelFileContentAsJsonWithLimits(bucketName, fileNames[0], inputX1, inputX2, inputY1, inputY2),
            "spc" => GetTablesFromSeveralSPC(bucketName, fileNames),
            _ => throw new ArgumentException("Unknown file extension.")
        };
    }
    private static void ValidateFiles(string[] files)
    {
        if (files == null || files.Length == 0 || files.Length > 1000 || files.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Provide 1..1000 file names.");
    }
}
