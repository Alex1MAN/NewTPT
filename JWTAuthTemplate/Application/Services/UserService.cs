using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.DTO.Identity;
using JWTAuthTemplate.Shared.Dtos;
using JWTAuthTemplate.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace JWTAuthTemplate.Application.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly IMinioService _minioService;


        public UserService(UserManager<ApplicationUser> userManager, ITokenService tokenService, IMinioService minioService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _minioService = minioService;
        }


        public async Task<(bool Success, string ErrorMessage)> Register([FromBody] RegisterDTO registration)
        {
            // Логика регистрации пользователя
            var userExists = await _userManager.FindByNameAsync(registration.Username);
            var emailExists = await _userManager.FindByEmailAsync(registration.Email);
            if (userExists != null)
            {
                return (false, "That username already exists!");
            }
            if (emailExists != null)
            {
                return (false, "That email is already in use!");
            }
            
            var user = new ApplicationUser()
            {
                Id = Guid.NewGuid().ToString(),
                Email = registration.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = registration.Username,
                CreateDate = DateTime.UtcNow,
            };

            // Добавление пользователя в базу данных
            var result = await _userManager.CreateAsync(user, registration.Password);
            if (!result.Succeeded)
            {
                return (false, string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            // Присвоение начальной роли - проверить также через if (!result.Succeeded)
            // var result = await _userManager.AddToRoleAsync(user, "User");

            // Создаем бакет в minio
            string bucketName = user.Id;
            try
            {
                await _minioService.CreateBucketAsync(bucketName);
                return (true, "User (and bucket in Minio) created successfully!");
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
        }


        public async Task<AuthResultDTO> Authenticate(LoginDTO login)
        {
            // Логика аутентификации пользователя
            var user = await _userManager.FindByNameAsync(login.Username);
            if (user == null)
            {
                throw new ValidationException("Invalid username or password!");
            }
            var result = await _userManager.CheckPasswordAsync(user, login.Password);
            if (!result)
            {
                throw new ValidationException("Invalid username or password!");
            }
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
            };
            var token = _tokenService.CreateToken(claims.ToList());
            var refreshToken = _tokenService.GenerateRefreshToken();

            // Обновление информации о пользователе с новым refresh токеном (если требуется)
            user.RefreshToken = refreshToken;
            await _userManager.UpdateAsync(user);
            return new AuthResultDTO
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                RefreshToken = refreshToken,
                User = new UserDTO
                {
                    Id = user.Id,
                    Username = user.UserName,
                    Email = user.Email,
                    CreateDate = user.CreateDate
                }
            };
        }


        public async Task<UserDTO?> GetByUsername(string username)
        {
            var user = await _userManager.Users.Where(u => u.UserName == username).Select(x => new UserDTO()
            {
                Id = x.Id,
                Username = x.UserName,
                Email = x.Email,
                CreateDate = x.CreateDate,
                Roles = new List<UserRoleDTO>(x.Roles.Select(r => new UserRoleDTO()
                {
                    Role = new RoleDTO()
                    {
                        Id = r.Role.Id,
                        Name = r.Role.Name,
                    }
                })),
            }).FirstOrDefaultAsync();
            if (user == null)
            {
                return null;
            }
            return user;
        }
    }
}
