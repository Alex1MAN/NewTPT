using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Models.Identity;
using Microsoft.EntityFrameworkCore;
namespace JWTAuthTemplate.Infrastructure.Database
{
    public class ReferencesRepository : IReferencesRepository
    {
        private readonly Context _context;
        public ReferencesRepository(Context context) => _context = context;
        public async Task AddReferenceInPostgre(UserReferencesInMinio reference)
        {
            if (!await _context.Users.AnyAsync(u => u.Id == reference.UserId))
                throw new KeyNotFoundException("User not found.");
            _context.UserReferencesInMinio.Add(reference);
            await _context.SaveChangesAsync();
        }
        public Task<List<UserReferencesInMinio>> GetFileReferencesByUserId(string userId)
            => _context.UserReferencesInMinio.AsNoTracking().Where(r => r.UserId == userId).ToListAsync();
    }
}
