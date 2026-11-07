using JWTAuthTemplate.Shared.Dtos;

namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>Регистрация, вход и поиск пользователей.</summary>
public interface IUserService
{
    Task<(bool Success, string ErrorMessage)> Register(RegisterDTO dto);
    Task<AuthResultDTO> Authenticate(LoginDTO dto);
    Task<UserDTO?> GetByUsername(string username);
}
