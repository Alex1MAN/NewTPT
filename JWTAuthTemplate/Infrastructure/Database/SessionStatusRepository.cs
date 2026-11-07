using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace JWTAuthTemplate.Infrastructure.Database;

/// <summary>Хранилище статусов сессий; фильтрация по столбцам FileName/FileExtension выполняется в БД.</summary>
public sealed class SessionStatusRepository : ISessionStatusRepository
{
    private readonly Context _context;

    public SessionStatusRepository(Context context) => _context = context;

    public Task<bool> UserExistsAsync(string userId) => _context.Users.AnyAsync(u => u.Id == userId);

    public async Task<int> AddAsync(UserSessionStatus status)
    {
        _context.UserSessionStatuses.Add(status);
        await _context.SaveChangesAsync();
        return status.Id;
    }

    public Task<UserSessionStatus?> GetLatestAsync(string userId, string? fileName = null, string? fileExtension = null, DateTime? asOf = null)
    {
        var query = fileName == null || fileExtension == null ? ByUser(userId) : ByFile(userId, fileName, fileExtension);
        if (asOf.HasValue)
            query = query.Where(s => s.ActualAt <= asOf.Value);
        return query.OrderByDescending(s => s.ActualAt).ThenByDescending(s => s.Id).FirstOrDefaultAsync();
    }

    public Task<UserSessionStatus?> GetFirstSinceAsync(string userId, string fileName, string fileExtension, DateTime since)
        => ByFile(userId, fileName, fileExtension)
            .Where(s => s.ActualAt >= since)
            .OrderBy(s => s.ActualAt).ThenBy(s => s.Id)
            .FirstOrDefaultAsync();

    public Task<List<UserSessionStatus>> GetByFileAsync(string userId, string fileName, string fileExtension)
        => ByFile(userId, fileName, fileExtension)
            .OrderByDescending(s => s.ActualAt).ThenByDescending(s => s.Id)
            .ToListAsync();

    private IQueryable<UserSessionStatus> ByUser(string userId)
        => _context.UserSessionStatuses.AsNoTracking().Where(s => s.UserId == userId);

    private IQueryable<UserSessionStatus> ByFile(string userId, string fileName, string fileExtension)
        => ByUser(userId).Where(s => s.FileName == fileName && s.FileExtension == fileExtension);
}
