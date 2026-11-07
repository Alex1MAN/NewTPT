using System.ComponentModel.DataAnnotations;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Shared.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JWTAuthTemplate.Infrastructure.Identity;

/// <summary>Регистрация, вход и поиск пользователей через ASP.NET Core Identity.</summary>
public sealed class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IObjectStorage _storage;

    public UserService(UserManager<ApplicationUser> userManager, ITokenService tokenService, IObjectStorage storage)
        => (_userManager, _tokenService, _storage) = (userManager, tokenService, storage);

    public async Task<(bool Success, string ErrorMessage)> Register(RegisterDTO registration)
    {
        if (await _userManager.FindByNameAsync(registration.Username) != null)
            return (false, "That username already exists!");
        if (await _userManager.FindByEmailAsync(registration.Email) != null)
            return (false, "That email is already in use!");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = registration.Email,
            SecurityStamp = Guid.NewGuid().ToString(),
            UserName = registration.Username,
            CreateDate = DateTime.UtcNow
        };
        var result = await _userManager.CreateAsync(user, registration.Password);
        if (!result.Succeeded)
            return (false, string.Join(", ", result.Errors.Select(e => e.Description)));

        // Бакет пользователя в MinIO называется его ID.
        try
        {
            await _storage.EnsureBucketAsync(user.Id);
            return (true, "User (and bucket in Minio) created successfully!");
        }
        catch (Exception e)
        {
            return (false, e.Message);
        }
    }

    public async Task<AuthResultDTO> Authenticate(LoginDTO login)
    {
        var user = await _userManager.FindByNameAsync(login.Username);
        if (user == null || !await _userManager.CheckPasswordAsync(user, login.Password))
            throw new ValidationException("Invalid username or password!");

        var tokens = await _tokenService.IssueTokensAsync(user);
        return new AuthResultDTO
        {
            Token = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            User = new UserDTO { Id = user.Id, Username = user.UserName, Email = user.Email, CreateDate = user.CreateDate }
        };
    }

    public Task<UserDTO?> GetByUsername(string username)
        => _userManager.Users.Where(u => u.UserName == username).Select(u => new UserDTO
        {
            Id = u.Id,
            Username = u.UserName,
            Email = u.Email,
            CreateDate = u.CreateDate,
            Roles = u.Roles.Select(r => new UserRoleDTO { Role = new RoleDTO { Id = r.Role!.Id, Name = r.Role.Name! } }).ToList()
        }).FirstOrDefaultAsync();
}
