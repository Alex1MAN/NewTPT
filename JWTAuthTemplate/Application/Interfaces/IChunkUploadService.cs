using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Загрузка больших файлов по частям.</summary>
public interface IChunkUploadService
{
    Task UploadChunk(UploadChunkRequest request, Stream chunk, long length, CancellationToken ct = default);
    Task<CompleteUploadResponse> CompleteUpload(CompleteUploadRequest request, CancellationToken ct = default);
}
