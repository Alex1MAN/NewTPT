using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace JWTAuthTemplate.Infrastructure.Database;

/// <summary>Хранилище документов, версий и legacy-ссылок в PostgreSQL.</summary>
public sealed class DocumentRepository : IDocumentRepository
{
    private readonly Context _context;

    public DocumentRepository(Context context) => _context = context;

    public Task<bool> UserExistsAsync(string userId) => _context.Users.AnyAsync(u => u.Id == userId);

    public async Task<IUnitOfWork> BeginTransactionAsync()
        => new UnitOfWork(_context, await _context.Database.BeginTransactionAsync());

    public Task<Document> AddDocumentAsync(string userId, string fileName)
        => DocumentWriter.AddDocumentAsync(_context, userId, fileName);

    public async Task<DocumentVersion> AddVersionAsync(Document document, int versionNumber, string objectName,
        string etag, long fileSize, string fileName)
    {
        var version = await DocumentWriter.AddVersionAsync(_context, document, versionNumber, objectName, etag, fileSize, fileName);
        await _context.SaveChangesAsync();
        return version;
    }

    public async Task AddLegacyReferenceAsync(Document document, string etag)
    {
        DocumentWriter.AddLegacyReference(_context, document, etag);
        await _context.SaveChangesAsync();
    }

    public Task<List<Document>> GetDocumentsByUserAsync(string userId)
        => _context.Documents.AsNoTracking()
            .Where(d => d.UserId == userId)
            .Include(d => d.CurrentVersion)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync();

    public Task<Document?> GetDocumentAsync(int documentId, bool includeVersions = false)
    {
        IQueryable<Document> query = _context.Documents.Include(d => d.CurrentVersion);
        if (includeVersions)
            query = query.Include(d => d.Versions);
        return query.FirstOrDefaultAsync(d => d.Id == documentId);
    }

    public Task<List<DocumentVersion>> GetVersionsAsync(int documentId)
        => _context.DocumentVersions.AsNoTracking()
            .Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync();

    public Task<DocumentVersion?> GetVersionAsync(int versionId)
        => _context.DocumentVersions.Include(v => v.Document).FirstOrDefaultAsync(v => v.Id == versionId);

    public Task<int> CountVersionsAsync(int documentId)
        => _context.DocumentVersions.CountAsync(v => v.DocumentId == documentId);

    public async Task DeleteDocumentAsync(Document document)
    {
        // Сначала разрываем цикл Document -> CurrentVersion -> Document, затем удаляем; всё в одной транзакции.
        await using var transaction = await _context.Database.BeginTransactionAsync();
        document.CurrentVersionId = null;
        document.CurrentVersion = null;
        await _context.SaveChangesAsync();
        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task DeleteVersionAsync(DocumentVersion version)
    {
        var document = version.Document;
        if (document.CurrentVersionId == version.Id)
        {
            // Текущей становится предыдущая по номеру версия.
            var previous = await _context.DocumentVersions
                .Where(v => v.DocumentId == version.DocumentId && v.Id != version.Id)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();
            document.CurrentVersionId = previous?.Id;
            document.UpdatedAt = DateTime.UtcNow;
        }
        _context.DocumentVersions.Remove(version);
        await _context.SaveChangesAsync();
    }

    public async Task AddReferenceAsync(UserReferencesInMinio reference)
    {
        if (!await UserExistsAsync(reference.UserId!))
            throw new KeyNotFoundException("User not found.");
        _context.UserReferencesInMinio.Add(reference);
        await _context.SaveChangesAsync();
    }

    public Task<List<UserReferencesInMinio>> GetReferencesAsync(string userId)
        => _context.UserReferencesInMinio.AsNoTracking().Where(r => r.UserId == userId).ToListAsync();

    private sealed class UnitOfWork : IUnitOfWork
    {
        private readonly Context _context;
        private readonly IDbContextTransaction _transaction;

        public UnitOfWork(Context context, IDbContextTransaction transaction) => (_context, _transaction) = (context, transaction);

        public async Task CommitAsync(CancellationToken ct = default)
        {
            await _context.SaveChangesAsync(ct);
            await _transaction.CommitAsync(ct);
        }

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
