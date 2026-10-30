using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace JWTAuthTemplate.Infrastructure.Database;
public sealed class LegacyUserRepository : ILegacyUserRepository
{
    private readonly Context _context;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    public LegacyUserRepository(Context context, UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles)
        => (_context, _users, _roles) = (context, users, roles);
    public async Task AddRole(string username, string roleName)
    {
        var user = await _users.FindByNameAsync(username) ?? throw new KeyNotFoundException("User not found.");
        if (!await _roles.RoleExistsAsync(roleName)) throw new KeyNotFoundException("Role not found.");
        var result = await _users.AddToRoleAsync(user, roleName);
        if (!result.Succeeded) throw new ArgumentException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
    public async Task RemoveRole(string username, string roleName)
    {
        var user = await _users.FindByNameAsync(username) ?? throw new KeyNotFoundException("User not found.");
        if (!await _roles.RoleExistsAsync(roleName)) throw new KeyNotFoundException("Role not found.");
        if (!await _users.IsInRoleAsync(user, roleName)) throw new KeyNotFoundException("User is not in role.");
        var result = await _users.RemoveFromRoleAsync(user, roleName);
        if (!result.Succeeded) throw new ArgumentException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
    public async Task SaveStatus(string userId, Dictionary<string, object> statusParams)
    {
        if (!await _context.Users.AnyAsync(u => u.Id == userId)) throw new KeyNotFoundException("User not found.");
        _context.UserSessionStatuses.Add(new UserSessionStatus
        {
            UserId = userId, StatusParamsDict = statusParams,
            FileName = statusParams.GetValueOrDefault("fileName")?.ToString() ?? "",
            FileExtension = statusParams.GetValueOrDefault("fileExtension")?.ToString() ?? ""
        });
        await _context.SaveChangesAsync();
    }
    public Task<List<UserSessionStatus>> GetStatuses(string userId)
        => _context.UserSessionStatuses.AsNoTracking().Where(s => s.UserId == userId)
            .OrderByDescending(s => s.ActualAt).ThenByDescending(s => s.Id).ToListAsync();
}
