namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Чтение содержимого Excel/SPC-файлов из хранилища в JSON-текст для клиента.</summary>
public interface IFileContentService
{
    Task<string> GetExcelContentAsync(string bucketName, string objectName);
    Task<string> GetExcelContentWithLimitsAsync(string bucketName, string objectName,
        double inputX1, double inputX2, double inputY1, double inputY2);
    Task<string> GetSpcContentAsync(string bucketName, string objectName);
    Task<string> GetTablesFromSeveralSpcAsync(string bucketName, string[] fileNames);
    Task<string> GetAllContentFromFilesAsync(string bucketName, string[] fileNames, string allFilesExtension,
        double inputX1, double inputX2, double inputY1, double inputY2);
}
