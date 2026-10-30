using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Services;
using JWTAuthTemplate.Models.Identity;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
namespace JWTAuthTemplate.WebAPI.Controllers;

public partial class MinioController
{
    [HttpPost("UploadChunk")]
    [RequestSizeLimit(ChunkUploadService.MaxChunkBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = ChunkUploadService.MaxChunkBytes + 65536,
        MemoryBufferThreshold = ChunkUploadService.MaxChunkBytes + 65536)]
    public Task<IActionResult> UploadChunk([FromForm] string bucketName, [FromForm] string uploadId,
        [FromForm] string fileName, [FromForm] int chunkIndex, [FromForm] int totalChunks,
        IFormFile? chunk, [FromServices] IChunkUploadService service, CancellationToken ct)
        => ExecuteSafeAsync(async () =>
        {
            if (chunk == null) throw new ArgumentException("Chunk missing.");
            using var stream = chunk.OpenReadStream();
            await service.UploadChunk(new UploadChunkRequest
            {
                BucketName = bucketName, UploadId = uploadId, FileName = fileName,
                ChunkIndex = chunkIndex, TotalChunks = totalChunks
            }, stream, chunk.Length, ct);
            return Ok(new { success = true, chunkIndex });
        });
    [HttpPost("CompleteUpload")]
    public Task<IActionResult> CompleteUpload([FromBody] CompleteUploadRequest req,
        [FromServices] IChunkUploadService service, CancellationToken ct)
        => ExecuteSafeAsync(async () => Ok(await service.CompleteUpload(req, ct)));

    [HttpPost("UploadFilesUpdateReferences")]
    public Task<IActionResult> UploadFilesUpdateReferences([FromForm] string bucketName, [FromForm] List<IFormFile> filesData)
        => ExecuteSafeAsync(async () => Ok(await _minioService.UploadFilesAsync(bucketName, filesData)));
    [HttpPost("UploadFilesUpdateReferences_2")]
    public Task<IActionResult> UploadFilesUpdateReferences_2([FromForm] string bucketName, [FromForm] List<IFormFile> filesData,
        [FromServices] IFileContentService service)
        => ExecuteSafeAsync(async () =>
        {
            await service.EnsureUniqueFiles(bucketName, filesData.Select(f => f.FileName).ToArray());
            return Ok(await _minioService.UploadFilesAsync(bucketName, filesData));
        });
    [HttpPost("TestUploadFile")]
    public Task<IActionResult> TestUploadFile(IFormFile file)
        => ExecuteSafeAsync(async () =>
        {
            if (file.Length == 0) throw new ArgumentException("File is required.");
            await _minioService.CreateBucketAsync("44444");
            using var stream = file.OpenReadStream();
            await _minioService.UploadFileAsync("44444", "test13", stream, file.Length);
            return Ok(true);
        });
    [HttpPost("AddReferenceInPostgre")]
    public Task<IActionResult> AddReferenceInPostgre([FromBody] UserReferencesInMinio userReferencesInMinio,
        [FromServices] IFileContentService service)
        => ExecuteSafeAsync(async () =>
        {
            await service.AddReferenceInPostgre(userReferencesInMinio);
            return CreatedAtAction(nameof(GetFileReferencesByUserId), new { userId = userReferencesInMinio.UserId }, userReferencesInMinio);
        });
    [HttpGet("GetFileReferencesByUserId")]
    public Task<IActionResult> GetFileReferencesByUserId(string userId, [FromServices] IFileContentService service)
        => ExecuteSafeAsync(async () => Ok(await service.GetFileReferencesByUserId(userId)));
    [HttpGet("GetUrlFileFromMinio")]
    public Task<IActionResult> GetUrlFileFromMinio(string bucketName, string objectName)
        => ExecuteSafeAsync(async () => Ok(new { Url = await _minioService.GetFileUrlAsync(bucketName, objectName) }));
    [HttpGet("GetAllTableFromExcel")]
    public Task<IActionResult> GetAllTableFromExcel(string bucketName, string fileName)
        => GetExcelFileContentAsJson(bucketName, fileName);
    [HttpGet("GetFileContent")]
    public Task<IActionResult> GetFileContent(string bucketName, string fileName)
        => ExecuteSafeAsync(async () =>
            File(await _minioService.GetFileAsync(bucketName, fileName), "application/octet-stream", Path.GetFileName(fileName)));
    [HttpGet("GetTableFromExcelWithLimits")]
    public Task<IActionResult> GetTableFromExcelWithLimits(string bucketName, string fileName,
        double inputX1, double inputX2, double inputY1, double inputY2)
        => GetExcelFileContentAsJsonWithLimits(bucketName, fileName, inputX1, inputX2, inputY1, inputY2);
    [HttpGet("GetTablesFromSeveralSPC")]
    public Task<IActionResult> GetTablesFromSeveralSPC(string bucketName, [FromQuery] string[] fileNames,
        [FromServices] IFileContentService service)
        => ExecuteSafeAsync(async () => Ok(await service.GetTablesFromSeveralSPC(bucketName, fileNames)));
    [HttpGet("GetAllContentFromFiles")]
    public Task<IActionResult> GetAllContentFromFiles(string bucketName, [FromQuery] string[] fileNames,
        string allFilesExtension, double inputX1, double inputX2, double inputY1, double inputY2,
        [FromServices] IFileContentService service)
        => ExecuteSafeAsync(async () => Ok(await service.GetAllContentFromFiles(bucketName, fileNames,
            allFilesExtension, inputX1, inputX2, inputY1, inputY2)));
}
