using System.Buffers.Binary;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace JWTAuthTemplate.Infrastructure.Database;
/// <summary>Сессии загрузки по частям; параллельные запросы одной сессии сериализуются pg_advisory_xact_lock.</summary>
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
            var document = await DocumentWriter.AddDocumentAsync(_context, session.BucketName, session.FileName, ct);
            await DocumentWriter.AddVersionAsync(_context, document, 1, session.ObjectName, etag, length, session.FileName, ct);
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
