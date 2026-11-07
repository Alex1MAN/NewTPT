using JWTAuthTemplate.Application.Exceptions;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JWTAuthTemplate.Infrastructure.Identity;

/// <summary>Управление ролями через ASP.NET Core Identity; общая логика для /roles и /User.</summary>
public sealed class RoleService : IRoleService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public RoleService(RoleManager<ApplicationRole> roleManager, UserManager<ApplicationUser> userManager)
        => (_roleManager, _userManager) = (roleManager, userManager);

    public async Task<bool> CreateRoleAsync(string roleName)
        => !await _roleManager.RoleExistsAsync(roleName)
           && (await _roleManager.CreateAsync(new ApplicationRole { Name = roleName })).Succeeded;

    public async Task<bool> DeleteRoleAsync(string roleName)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        return role != null && (await _roleManager.DeleteAsync(role)).Succeeded;
    }

    public Task<List<string>> GetAllRolesAsync() => _roleManager.Roles.Select(r => r.Name!).ToListAsync();

    public async Task AssignRoleAsync(string userId, string roleName)
    {
        var user = await FindUserWithRoleAsync(userId, roleName);
        if (await _userManager.IsInRoleAsync(user, roleName))
            throw new ConflictException($"Role '{roleName}' assigned before.");
        EnsureSucceeded(await _userManager.AddToRoleAsync(user, roleName), $"Error in assigning role '{roleName}'.");
    }

    public async Task RemoveRoleAsync(string userId, string roleName)
    {
        var user = await FindUserWithRoleAsync(userId, roleName);
        if (!await _userManager.IsInRoleAsync(user, roleName))
            throw new ConflictException($"Role '{roleName}' deleted before.");
        EnsureSucceeded(await _userManager.RemoveFromRoleAsync(user, roleName), $"Error in deleting role '{roleName}'.");
    }

    public async Task<List<string>> GetUserRolesAsync(string userId)
        => (await FindUserAsync(userId) is { } user ? await _userManager.GetRolesAsync(user) : throw new KeyNotFoundException("User not found.")).ToList();

    private Task<ApplicationUser?> FindUserAsync(string userId) => _userManager.FindByIdAsync(userId);

    private async Task<ApplicationUser> FindUserWithRoleAsync(string userId, string roleName)
    {
        var user = await FindUserAsync(userId) ?? throw new KeyNotFoundException("User not found.");
        if (!await _roleManager.RoleExistsAsync(roleName))
            throw new KeyNotFoundException("Role not found.");
        return user;
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
            throw new ArgumentException(message);
    }
}
