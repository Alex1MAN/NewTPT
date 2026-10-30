using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Shared.Dtos;
namespace JWTAuthTemplate.Application.Services;

public sealed class UploadConflictException : Exception
{
    public UploadConflictException(string message) : base(message) { }
}
public sealed class ChunkUploadService : IChunkUploadService
{
    public const int MaxChunkBytes = 8 * 1024 * 1024;
    public const int MaxChunks = 10000;
    public const long MaxFileBytes = 10L * 1024 * 1024 * 1024;
    private readonly IUploadRepository _repository;
    private readonly IObjectStorage _storage;
    public ChunkUploadService(IUploadRepository repository, IObjectStorage storage)
        => (_repository, _storage) = (repository, storage);
    private static string Validate(string bucket, string id, string file, int count)
    {
        if (string.IsNullOrWhiteSpace(bucket) || !Regex.IsMatch(bucket, @"^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$"))
            throw new ArgumentException("Invalid BucketName.");
        if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, @"^[A-Za-z0-9_-]{1,128}$"))
            throw new ArgumentException("Invalid UploadId.");
        if (string.IsNullOrWhiteSpace(file) || file.Length > 255 || file is "." or ".." ||
            file.Any(c => char.IsControl(c) || "/\\:".Contains(c)))
            throw new ArgumentException("FileName must be a file name, not a path.");
        if (count < 1 || count > MaxChunks) throw new ArgumentException("Invalid TotalChunks.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bucket + "\n" + id))).ToLowerInvariant();
    }
    private static void Match(ChunkUploadSession session, string fileName, int count)
    {
        if (session.FileName != fileName || session.TotalChunks != count)
            throw new UploadConflictException("UploadId already belongs to another file or chunk count.");
    }
    private static string PartName(string key, int index) => $"_chunks/{key}/{index}";
    public async Task UploadChunk(UploadChunkRequest request, Stream chunk, long length, CancellationToken ct = default)
    {
        var key = Validate(request.BucketName, request.UploadId, request.FileName, request.TotalChunks);
        if (request.ChunkIndex < 0 || request.ChunkIndex >= request.TotalChunks)
            throw new ArgumentException("Invalid ChunkIndex.");
        if (length < 1 || length > MaxChunkBytes) throw new ArgumentException("Chunk size must be 1..8 MiB.");
        var bytes = new byte[checked((int)length)];
        await chunk.ReadExactlyAsync(bytes, ct);
        if (await chunk.ReadAsync(new byte[1], ct) != 0) throw new ArgumentException("Incorrect chunk length.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        await using var tx = await _repository.Begin(key, ct);
        var session = await tx.Get(key, ct);
        if (session == null)
        {
            if (!await tx.UserExists(request.BucketName, ct)) throw new KeyNotFoundException("User not found.");
            session = new ChunkUploadSession
            {
                Key = key, BucketName = request.BucketName, UploadId = request.UploadId,
                FileName = request.FileName, TotalChunks = request.TotalChunks,
                ObjectName = $"{request.BucketName}/uploads/{key}/{request.FileName}"
            };
            tx.Add(session);
        }
        Match(session, request.FileName, request.TotalChunks);
        var old = session.Parts.SingleOrDefault(p => p.ChunkIndex == request.ChunkIndex);
        if (old != null)
        {
            if (old.Length != length || old.Sha256 != hash)
                throw new UploadConflictException("A different chunk already exists at this index.");
            return;
        }
        if (session.Completed) throw new UploadConflictException("Upload already completed.");
        if (session.Parts.Sum(p => p.Length) + length > MaxFileBytes)
            throw new ArgumentException("File exceeds 10 GiB.");
        using var input = new MemoryStream(bytes, writable: false);
        await _storage.Put(session.BucketName, PartName(key, request.ChunkIndex), input, length, ct);
        session.Parts.Add(new UploadPart { SessionKey = key, ChunkIndex = request.ChunkIndex, Length = length, Sha256 = hash });
        session.UpdatedAt = DateTime.UtcNow;
        await tx.Commit(ct);
    }
    public async Task<CompleteUploadResponse> CompleteUpload(CompleteUploadRequest request, CancellationToken ct = default)
    {
        var key = Validate(request.BucketName, request.UploadId, request.FileName, request.TotalChunks);
        CompleteUploadResponse response;
        await using (var tx = await _repository.Begin(key, ct))
        {
            var session = await tx.Get(key, ct) ?? throw new KeyNotFoundException("Upload session not found.");
            Match(session, request.FileName, request.TotalChunks);
            if (!session.Completed)
            {
                var parts = session.Parts.OrderBy(p => p.ChunkIndex).ToArray();
                if (parts.Length != session.TotalChunks || parts.Where((p, i) => p.ChunkIndex != i).Any())
                    throw new UploadConflictException("Missing chunks. Upload them before CompleteUpload.");
                var length = parts.Sum(p => p.Length);
                using var stream = new OrderedChunkStream(_storage, session.BucketName, key, parts, ct);
                await _storage.Put(session.BucketName, session.ObjectName, stream, length, ct);
                session.ETag = await _storage.ETag(session.BucketName, session.ObjectName, ct);
                session.DocumentId = await tx.AddDocument(session, length, session.ETag, ct);
                session.Completed = true;
                session.UpdatedAt = DateTime.UtcNow;
                await tx.Commit(ct);
            }
            response = new CompleteUploadResponse
            {
                Success = true, FileName = session.FileName, FileReferenceMinio = session.ETag,
                DocumentId = session.DocumentId!.Value, MinioObjectName = session.ObjectName
            };
        }
        // Повтор CompleteUpload повторяет уборку, но не создаёт второй документ.
        for (int i = 0; i < request.TotalChunks; i++)
        {
            try { await _storage.Delete(request.BucketName, PartName(key, i), ct); }
            catch when (!ct.IsCancellationRequested) { /* Без потери квитанции успешной загрузки. */ }
        }
        return response;
    }
    private sealed class OrderedChunkStream : Stream
    {
        private readonly IObjectStorage _storage;
        private readonly string _bucket, _key;
        private readonly UploadPart[] _parts;
        private readonly CancellationToken _ct;
        private int _index, _offset;
        private byte[] _current = Array.Empty<byte>();
        public OrderedChunkStream(IObjectStorage storage, string bucket, string key, UploadPart[] parts, CancellationToken ct)
            => (_storage, _bucket, _key, _parts, _ct) = (storage, bucket, key, parts, ct);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _ct.ThrowIfCancellationRequested();
            cancellationToken.ThrowIfCancellationRequested();
            if (buffer.Length == 0) return 0;
            if (_offset == _current.Length)
            {
                if (_index == _parts.Length) return 0;
                var part = _parts[_index++];
                _current = await _storage.Read(_bucket, PartName(_key, part.ChunkIndex), _ct);
                if (_current.LongLength != part.Length || Convert.ToHexString(SHA256.HashData(_current)) != part.Sha256)
                    throw new IOException("Chunk integrity check failed.");
                _offset = 0;
            }
            int count = Math.Min(buffer.Length, _current.Length - _offset);
            _current.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            return count;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer.AsMemory(offset, count), _ct).AsTask().GetAwaiter().GetResult();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _parts.Sum(p => p.Length);
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
