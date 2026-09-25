using Elchwinkel.Spc;
using ExcelDataReader;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.DTO.Identity;
using JWTAuthTemplate.Infrastructure.Database;
using JWTAuthTemplate.Models.Identity;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;
using System.Data;
using System.Text;
using System.Threading.Tasks;

namespace JWTAuthTemplate.Application.Services;

public class MinioService : IMinioService
{
    private readonly MinioClient _minioClient;
    private readonly Context _context;

    public MinioService(IOptions<MinioSettingsDTO> minioSettings, Context context)
    {
        _minioClient = (MinioClient?)new MinioClient()
            .WithEndpoint(minioSettings.Value.Endpoint)
            .WithCredentials(minioSettings.Value.AccessKey, minioSettings.Value.SecretKey)
            .Build() ?? throw new InvalidOperationException("MinioClient initialization failed");
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region Работа с бакетами

    public async Task CreateBucketAsync(string bucketName)
    {
        var args = new BucketExistsArgs().WithBucket(bucketName);
        bool found = await _minioClient.BucketExistsAsync(args);
        if (!found)
        {
            await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
        }
    }

    #endregion

    #region Работа с файлами в MinIO

    public async Task<string> GetObjectETagAsync(string bucketName, string objectName)
    {
        try
        {
            var statArgs = new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);
            var objectStat = await _minioClient.StatObjectAsync(statArgs);
            return objectStat.ETag;
        }
        catch (Exception ex)
        {
            throw new Exception($"Couldn't get the ETAG for {objectName}", ex);
        }
    }

    public async Task<Stream> GetFileAsync(string bucketName, string objectName)
    {
        var memoryStream = new MemoryStream();
        var getObjectArgs = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream =>
            {
                stream.CopyTo(memoryStream);
            });
        await _minioClient.GetObjectAsync(getObjectArgs);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task UploadFileAsync(string bucketName, string objectName, Stream fileStream, long length)
    {
        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(fileStream)
            .WithObjectSize(length)
            .WithContentType("application/octet-stream"));
    }

    public async Task<string> GetFileUrlAsync(string bucketName, string objectName)
    {
        var stream = await GetFileAsync(bucketName, objectName);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    #endregion

    #region Работа с документами и версиями

    public async Task<UploadResultDTO> UploadFilesAsync(string bucketName, List<IFormFile> filesData)
    {
        if (filesData == null || !filesData.Any())
        {
            throw new ArgumentException("No files provided for upload");
        }

        var result = new UploadResultDTO
        {
            BucketName = bucketName,
            References = new List<FileReferenceDTO>()
        };

        foreach (var fileData in filesData)
        {
            if (fileData == null || fileData.Length == 0)
            {
                continue;
            }

            var fileName = fileData.FileName;
            var fileExtension = Path.GetExtension(fileName).Replace(".", "");

            // Создаём документ
            var document = new Document
            {
                UserId = bucketName,
                OriginalName = fileName,
                FileExtension = fileExtension,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            // Генерируем путь в MinIO: {userId}/{documentId}/v1_{fileName}
            var minioObjectName = $"{bucketName}/{document.Id}/v1_{fileName}";

            // Загружаем файл в MinIO
            using var memoryStream = fileData.OpenReadStream();
            await UploadFileAsync(bucketName, minioObjectName, memoryStream, fileData.Length);

            // Получаем ETag
            var etag = await GetObjectETagAsync(bucketName, minioObjectName);

            // Создаём первую версию
            var version = new DocumentVersion
            {
                DocumentId = document.Id,
                VersionNumber = 1,
                MinioObjectName = minioObjectName,
                ETag = etag,
                FileSize = fileData.Length,
                OriginalFileName = fileName,
                CreatedAt = DateTime.UtcNow
            };

            _context.DocumentVersions.Add(version);
            await _context.SaveChangesAsync();

            // Обновляем CurrentVersionId у документа
            document.CurrentVersionId = version.Id;
            await _context.SaveChangesAsync();

            // Добавляем в результат
            result.References.Add(new FileReferenceDTO
            {
                DocumentId = document.Id,
                FileName = fileName,
                FileExtension = fileExtension,
                CurrentVersionNumber = 1,
                CreatedAt = document.CreatedAt,
                UpdatedAt = document.UpdatedAt
            });
        }

        result.Count = result.References.Count;
        return result;
    }

    public async Task<List<FileReferenceDTO>> GetDocumentsByUserIdAsync(string userId)
    {
        var documents = await _context.Documents
            .Where(d => d.UserId == userId)
            .Include(d => d.CurrentVersion)
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new FileReferenceDTO
            {
                DocumentId = d.Id,
                FileName = d.OriginalName,
                FileExtension = d.FileExtension,
                CurrentVersionNumber = d.CurrentVersion != null ? d.CurrentVersion.VersionNumber : 0,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .ToListAsync();

        return documents;
    }

    public async Task<FileReferenceDTO?> GetDocumentByIdAsync(int documentId)
    {
        var document = await _context.Documents
            .Include(d => d.CurrentVersion)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            return null;
        }

        return new FileReferenceDTO
        {
            DocumentId = document.Id,
            FileName = document.OriginalName,
            FileExtension = document.FileExtension,
            CurrentVersionNumber = document.CurrentVersion?.VersionNumber ?? 0,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        };
    }

    public async Task<List<DocumentVersionDTO>> GetDocumentVersionsAsync(int documentId)
    {
        var versions = await _context.DocumentVersions
            .Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DocumentVersionDTO
            {
                VersionId = v.Id,
                DocumentId = v.DocumentId,
                VersionNumber = v.VersionNumber,
                MinioObjectName = v.MinioObjectName,
                FileSize = v.FileSize,
                OriginalFileName = v.OriginalFileName,
                CreatedAt = v.CreatedAt
            })
            .ToListAsync();

        return versions;
    }

    public async Task<DocumentVersionDTO?> GetDocumentVersionByIdAsync(int versionId)
    {
        var version = await _context.DocumentVersions
            .FirstOrDefaultAsync(v => v.Id == versionId);

        if (version == null)
        {
            return null;
        }

        return new DocumentVersionDTO
        {
            VersionId = version.Id,
            DocumentId = version.DocumentId,
            VersionNumber = version.VersionNumber,
            MinioObjectName = version.MinioObjectName,
            FileSize = version.FileSize,
            OriginalFileName = version.OriginalFileName,
            CreatedAt = version.CreatedAt
        };
    }

    public async Task<DocumentVersionDTO> CreateNewVersionAsync(int documentId, IFormFile file)
    {
        var document = await _context.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new ArgumentException($"Document with ID {documentId} not found");
        }

        var fileName = file.FileName;
        var fileExtension = Path.GetExtension(fileName).Replace(".", "");

        // Проверяем, что расширение совпадает
        if (!string.Equals(document.FileExtension, fileExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"File extension mismatch. Expected: {document.FileExtension}, got: {fileExtension}");
        }

        // Определяем номер новой версии
        var maxVersionNumber = document.Versions.Max(v => v.VersionNumber);
        var newVersionNumber = maxVersionNumber + 1;

        // Генерируем путь в MinIO: {userId}/{documentId}/v{N}_{fileName}
        var minioObjectName = $"{document.UserId}/{document.Id}/v{newVersionNumber}_{fileName}";

        // Загружаем файл в MinIO
        using var memoryStream = file.OpenReadStream();
        await UploadFileAsync(document.UserId, minioObjectName, memoryStream, file.Length);

        // Получаем ETag
        var etag = await GetObjectETagAsync(document.UserId, minioObjectName);

        // Создаём новую версию
        var version = new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = newVersionNumber,
            MinioObjectName = minioObjectName,
            ETag = etag,
            FileSize = file.Length,
            OriginalFileName = fileName,
            CreatedAt = DateTime.UtcNow
        };

        _context.DocumentVersions.Add(version);
        await _context.SaveChangesAsync();

        // Обновляем CurrentVersionId у документа
        document.CurrentVersionId = version.Id;
        document.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new DocumentVersionDTO
        {
            VersionId = version.Id,
            DocumentId = version.DocumentId,
            VersionNumber = version.VersionNumber,
            MinioObjectName = version.MinioObjectName,
            FileSize = version.FileSize,
            OriginalFileName = version.OriginalFileName,
            CreatedAt = version.CreatedAt
        };
    }

    public async Task DeleteDocumentAsync(int documentId)
    {
        var document = await _context.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new ArgumentException($"Document with ID {documentId} not found");
        }

        // Удаляем все версии из MinIO
        foreach (var version in document.Versions)
        {
            try
            {
                var removeObjectArgs = new RemoveObjectArgs()
                    .WithBucket(document.UserId)
                    .WithObject(version.MinioObjectName);
                await _minioClient.RemoveObjectAsync(removeObjectArgs);
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем удаление
                Console.WriteLine($"Failed to delete object {version.MinioObjectName} from MinIO: {ex.Message}");
            }
        }

        // Удаляем документ из БД (каскадно удалятся все версии)
        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteDocumentVersionAsync(int versionId)
    {
        var version = await _context.DocumentVersions
            .Include(v => v.Document)
            .FirstOrDefaultAsync(v => v.Id == versionId);

        if (version == null)
        {
            throw new ArgumentException($"Version with ID {versionId} not found");
        }

        // Проверяем, что это не единственная версия
        var versionsCount = await _context.DocumentVersions
            .CountAsync(v => v.DocumentId == version.DocumentId);

        if (versionsCount == 1)
        {
            throw new InvalidOperationException("Cannot delete the last version of a document. Delete the entire document instead.");
        }

        // Удаляем файл из MinIO
        try
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(version.Document.UserId)
                .WithObject(version.MinioObjectName);
            await _minioClient.RemoveObjectAsync(removeObjectArgs);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete object {version.MinioObjectName} from MinIO: {ex.Message}");
        }

        // Если удаляем текущую версию, обновляем CurrentVersionId
        if (version.Document.CurrentVersionId == versionId)
        {
            var previousVersion = await _context.DocumentVersions
                .Where(v => v.DocumentId == version.DocumentId && v.Id != versionId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();

            version.Document.CurrentVersionId = previousVersion?.Id;
            version.Document.UpdatedAt = DateTime.UtcNow;
        }

        // Удаляем версию из БД
        _context.DocumentVersions.Remove(version);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Парсинг содержимого файлов

    public async Task<string> GetExcelFileContentAsJson(string bucketName, string objectName)
    {
        var memoryStream = new MemoryStream();
        var getObjectArgs = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream =>
            {
                stream.CopyTo(memoryStream);
            });
        await _minioClient.GetObjectAsync(getObjectArgs);
        memoryStream.Position = 0;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(memoryStream);
        var result = reader.AsDataSet();
        var table = result.Tables[0];

        var labels = new List<string>();
        for (int col = 0; col < table.Columns.Count; col++)
            labels.Add(table.Rows[0][col]?.ToString() ?? "");

        var sb = new StringBuilder();
        sb.Append("{\n  \"labels\": [");
        for (int i = 0; i < labels.Count; i++)
        {
            sb.Append($"\"{EscapeJson(labels[i])}\"");
            if (i < labels.Count - 1)
                sb.Append(", ");
        }
        sb.Append("],\n  \"values\": [\n    [\n");

        for (int row = 1; row < table.Rows.Count; row++)
        {
            sb.Append("      {");
            for (int col = 0; col < table.Columns.Count; col++)
            {
                string label = EscapeJson(labels[col]);
                string cellValue = EscapeJson(table.Rows[row][col]?.ToString() ?? "");
                sb.Append($"\"{label}\": \"{cellValue}\"");
                if (col < table.Columns.Count - 1)
                    sb.Append(", ");
            }
            sb.Append("}");
            if (row < table.Rows.Count - 1)
                sb.Append(",\n");
            else
                sb.Append("\n");
        }
        sb.Append("    ]\n  ]\n}");
        return sb.ToString();
    }

    public async Task<string> GetExcelFileContentAsJsonWithLimits(string bucketName, string objectName, double inputX1, double inputX2, double inputY1, double inputY2)
    {
        var memoryStream = new MemoryStream();
        var getObjectArgs = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream =>
            {
                stream.CopyTo(memoryStream);
            });
        await _minioClient.GetObjectAsync(getObjectArgs);
        memoryStream.Position = 0;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(memoryStream);
        var result = reader.AsDataSet();
        var table = result.Tables[0];

        var labels = new List<string>();
        var labelDoubles = new List<double?>();
        for (int col = 0; col < table.Columns.Count; col++)
        {
            string labelStr = table.Rows[0][col]?.ToString() ?? "";
            labels.Add(labelStr);
            if (double.TryParse(labelStr, out double d))
                labelDoubles.Add(d);
            else
                labelDoubles.Add(null);
        }

        var filteredCols = new List<int>();
        double minX = Math.Min(inputX1, inputX2);
        double maxX = Math.Max(inputX1, inputX2);
        for (int i = 0; i < labelDoubles.Count; i++)
        {
            if (labelDoubles[i].HasValue)
            {
                double val = labelDoubles[i].Value;
                if (val >= minX && val <= maxX)
                    filteredCols.Add(i);
            }
        }

        double minY = Math.Min(inputY1, inputY2);
        double maxY = Math.Max(inputY1, inputY2);

        var sb = new StringBuilder();
        sb.Append("{\n  \"labels\": [");
        for (int i = 0; i < filteredCols.Count; i++)
        {
            sb.Append($"\"{EscapeJson(labels[filteredCols[i]])}\"");
            if (i < filteredCols.Count - 1)
                sb.Append(", ");
        }
        sb.Append("],\n  \"values\": [\n    [\n");

        for (int row = 1; row < table.Rows.Count; row++)
        {
            sb.Append("      {");
            for (int i = 0; i < filteredCols.Count; i++)
            {
                int col = filteredCols[i];
                string label = EscapeJson(labels[col]);
                string cellStr = table.Rows[row][col]?.ToString() ?? "";

                if (double.TryParse(cellStr, out double cellValue))
                {
                    if (cellValue < minY)
                        cellValue = minY;
                    else if (cellValue > maxY)
                        cellValue = maxY;
                    cellStr = cellValue.ToString(System.Globalization.CultureInfo.CurrentCulture);
                }

                sb.Append($"\"{label}\": \"{EscapeJson(cellStr)}\"");
                if (i < filteredCols.Count - 1)
                    sb.Append(", ");
            }
            sb.Append("}");
            if (row < table.Rows.Count - 1)
                sb.Append(",\n");
            else
                sb.Append("\n");
        }
        sb.Append("    ]\n  ]\n}");
        return sb.ToString();
    }

    public async Task<string> GetSPCFileContentAsJson(string bucketName, string objectName)
    {
        var memoryStream = new MemoryStream();
        var getObjectArgs = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream =>
            {
                stream.CopyTo(memoryStream);
            });
        await _minioClient.GetObjectAsync(getObjectArgs);
        memoryStream.Position = 0;

        byte[] spcBytes = memoryStream.ToArray();
        var spc = SpcReader.Read(spcBytes);

        var sb = new StringBuilder();
        var labels = new List<string> { spc.XUnit.Name, spc.YUnit.Name };
        var xValues = spc.Spectra[0].X;
        var yValues = spc.Spectra[0].Y;

        sb.Append("{\n  \"labels\": [");
        for (int i = 0; i < labels.Count; i++)
        {
            sb.Append($"\"{EscapeJson(labels[i])}\"");
            if (i < labels.Count - 1)
                sb.Append(", ");
        }
        sb.Append("],\n  \"values\": [\n    [\n");

        for (int i = 0; i < xValues.Length; i++)
        {
            sb.Append("      {");
            sb.Append($"\"{EscapeJson(labels[0])}\": \"{xValues[i]}\"");
            sb.Append(", ");
            sb.Append($"\"{EscapeJson(labels[1])}\": \"{yValues[i]}\"");
            sb.Append("}");
            if (i < xValues.Length - 1)
                sb.Append(",\n");
            else
                sb.Append("\n");
        }
        sb.Append("    ]\n  ]\n}");
        return sb.ToString();
    }

    #endregion

    #region Вспомогательные методы

    private string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        return s.Replace("\\", "\\\\")
               .Replace("\"", "\\\"")
               .Replace("\n", "\\n")
               .Replace("\r", "\\r");
    }

    #endregion
}