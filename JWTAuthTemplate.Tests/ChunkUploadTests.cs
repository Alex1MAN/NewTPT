using JWTAuthTemplate.Application.Exceptions;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Application.Services;
using JWTAuthTemplate.Shared.Dtos;
using Moq;
using Xunit;
namespace JWTAuthTemplate.Tests;
/// <summary>Загрузка по частям: порядок, идемпотентность, конфликты и сбои хранилища.</summary>
public class ChunkUploadTests
{
    private readonly Mock<IUploadRepository> _repo = new();
    private readonly Mock<IUploadTransaction> _tx = new();
    private readonly Mock<IObjectStorage> _storage = new();
    private ChunkUploadSession? _session;
    private readonly Dictionary<string, byte[]> _objects = new();
    private readonly ChunkUploadService _service;
    public ChunkUploadTests()
    {
        _repo.Setup(r => r.Begin(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(_tx.Object);
        _tx.Setup(t => t.Get(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _session);
        _tx.Setup(t => t.UserExists(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _tx.Setup(t => t.Add(It.IsAny<ChunkUploadSession>())).Callback<ChunkUploadSession>(s => _session = s);
        _tx.Setup(t => t.AddDocument(It.IsAny<ChunkUploadSession>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(42);
        _storage.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(async (string bucket, string name, Stream stream, long length, CancellationToken ct) =>
            {
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, ct);
                Assert.Equal(length, memory.Length);
                _objects[name] = memory.ToArray();
            });
        _storage.Setup(s => s.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string bucket, string name, long? maxBytes, CancellationToken ct) => _objects[name]);
        _storage.Setup(s => s.GetETagAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("etag");
        _service = new ChunkUploadService(_repo.Object, _storage.Object);
    }
    private static UploadChunkRequest Request(int index = 0, int count = 2) => new()
    { BucketName = "test-user", UploadId = "upload-1", FileName = "test.bin", ChunkIndex = index, TotalChunks = count };
    private static CompleteUploadRequest Complete(int count = 2) => new()
    { BucketName = "test-user", UploadId = "upload-1", FileName = "test.bin", TotalChunks = count };
    private Task Upload(int index, byte[] bytes, int count = 2) => _service.UploadChunk(Request(index, count), new MemoryStream(bytes), bytes.Length);
    [Fact]
    public async Task OutOfOrderChunksAssembleCorrectlyAndCompleteIsIdempotent()
    {
        await Upload(1, new byte[] { 3, 4 });
        await Upload(0, new byte[] { 1, 2 });
        var result = await _service.CompleteUpload(Complete());
        Assert.True(result.Success);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, _objects[result.MinioObjectName]);
        Assert.Equal(42, (await _service.CompleteUpload(Complete())).DocumentId);
        _tx.Verify(t => t.AddDocument(It.IsAny<ChunkUploadSession>(), 4, "etag", It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task DuplicateIdenticalChunkIsIdempotent()
    {
        await Upload(0, new byte[] { 1, 2 });
        await Upload(0, new byte[] { 1, 2 });
        Assert.Single(_session!.Parts);
        _storage.Verify(s => s.PutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task DifferentDuplicateRejected()
    {
        await Upload(0, new byte[] { 1 });
        await Assert.ThrowsAsync<ConflictException>(() => Upload(0, new byte[] { 2 }));
    }
    [Fact]
    public async Task MissingChunksDoNotFinalizeAndCanBeRetried()
    {
        await Upload(0, new byte[] { 1 });
        await Assert.ThrowsAsync<ConflictException>(() => _service.CompleteUpload(Complete()));
        Assert.False(_session!.Completed);
        await Upload(1, new byte[] { 2 });
        Assert.True((await _service.CompleteUpload(Complete())).Success);
    }
    [Fact]
    public async Task MismatchedMetadataRejected()
    {
        await Upload(0, new byte[] { 1 });
        var req = Request(); req.FileName = "other.bin";
        await Assert.ThrowsAsync<ConflictException>(() => _service.UploadChunk(req, new MemoryStream(new byte[] { 1 }), 1));
        await Assert.ThrowsAsync<ConflictException>(() => _service.CompleteUpload(Complete(3)));
    }
    [Theory]
    [InlineData(-1, 2)]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    [InlineData(0, 10001)]
    public async Task InvalidIndexesAndCountsRejected(int index, int count)
        => await Assert.ThrowsAsync<ArgumentException>(() => Upload(index, new byte[] { 1 }, count));
    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("x/y")]
    public async Task PathTraversalRejected(string file)
    {
        var req = Request(); req.FileName = file;
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UploadChunk(req, new MemoryStream(new byte[] { 1 }), 1));
    }
    [Fact]
    public async Task OversizedAndEmptyChunkRejectedBeforeStorage()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UploadChunk(Request(), Stream.Null, ChunkUploadService.MaxChunkBytes + 1));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UploadChunk(Request(), Stream.Null, 0));
        _repo.Verify(r => r.Begin(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task UnknownSessionAndUnknownUserRejected()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CompleteUpload(Complete()));
        _tx.Setup(t => t.UserExists(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Upload(0, new byte[] { 1 }));
    }
    [Fact]
    public async Task CorruptedStorageChunkRejectedBeforeDatabaseCompletion()
    {
        await Upload(0, new byte[] { 1 }, 1);
        _objects[_objects.Keys.Single()] = new byte[] { 2 };
        await Assert.ThrowsAsync<IOException>(() => _service.CompleteUpload(Complete(1)));
        _tx.Verify(t => t.AddDocument(It.IsAny<ChunkUploadSession>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task FailedStoragePutDoesNotCommit()
    {
        _storage.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("offline"));
        await Assert.ThrowsAsync<IOException>(() => Upload(0, new byte[] { 1 }));
        _tx.Verify(t => t.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task CleanupFailureDoesNotLoseCompletedReceipt()
    {
        await Upload(0, new byte[] { 1 }, 1);
        _storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("offline"));
        Assert.True((await _service.CompleteUpload(Complete(1))).Success);
        Assert.True((await _service.CompleteUpload(Complete(1))).Success);
    }
}
