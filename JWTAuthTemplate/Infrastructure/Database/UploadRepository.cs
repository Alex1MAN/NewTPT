using System.Buffers.Binary;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace JWTAuthTemplate.Infrastructure.Database;
public sealed class UploadRepository : IUploadRepository
{
    private readonly Context _context;
    public UploadRepository(Context context) => _context = context;
    public async Task<IUploadTransaction> Begin(string key, CancellationToken ct)
    {
        var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            long lockId = BinaryPrimitives.ReadInt64BigEndian(Convert.FromHexString(key));
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", ct);
            return new Transaction(_context, tx);
        }
        catch { await tx.DisposeAsync(); throw; }
    }
    private sealed class Transaction : IUploadTransaction
    {
        private readonly Context _context;
        private readonly IDbContextTransaction _tx;
        public Transaction(Context context, IDbContextTransaction tx) => (_context, _tx) = (context, tx);
        public Task<ChunkUploadSession?> Get(string key, CancellationToken ct)
            => _context.ChunkUploadSessions.Include(s => s.Parts).SingleOrDefaultAsync(s => s.Key == key, ct);
        public Task<bool> UserExists(string userId, CancellationToken ct)
            => _context.Users.AnyAsync(u => u.Id == userId, ct);
        public void Add(ChunkUploadSession session) => _context.ChunkUploadSessions.Add(session);
        public async Task<int> AddDocument(ChunkUploadSession session, long length, string etag, CancellationToken ct)
        {
            var document = new Document
            {
                UserId = session.BucketName, OriginalName = session.FileName,
                FileExtension = Path.GetExtension(session.FileName).TrimStart('.')
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync(ct);
            var version = new DocumentVersion
            {
                DocumentId = document.Id, VersionNumber = 1, MinioObjectName = session.ObjectName,
                ETag = etag, FileSize = length, OriginalFileName = session.FileName
            };
            _context.DocumentVersions.Add(version);
            await _context.SaveChangesAsync(ct);
            document.CurrentVersionId = version.Id;
            _context.UserReferencesInMinio.Add(new UserReferencesInMinio
            {
                UserId = session.BucketName, FileName = session.FileName,
                FileExtension = document.FileExtension, FileReferenceMinio = etag
            });
            return document.Id;
        }
        public async Task Commit(CancellationToken ct)
        {
            await _context.SaveChangesAsync(ct);
            await _tx.CommitAsync(ct);
        }
        public ValueTask DisposeAsync() => _tx.DisposeAsync();
    }
}
