namespace JWTAuthTemplate.Application.Interfaces;

/// <summary>
/// Управление ролями. Примеры ролей:
/// Administrator — полный контроль над системой;
/// Moderator — ограниченный административный доступ;
/// User — обычный зарегистрированный пользователь;
/// Guest — незарегистрированный пользователь.
/// </summary>
public interface IRoleService
{
    /// <summary>false, если роль уже существует или не создана.</summary>
    Task<bool> CreateRoleAsync(string roleName);

    /// <summary>false, если роли нет.</summary>
    Task<bool> DeleteRoleAsync(string roleName);

    Task<List<string>> GetAllRolesAsync();

    /// <summary>Назначает роль. KeyNotFound — нет пользователя/роли, Conflict — роль уже назначена.</summary>
    Task AssignRoleAsync(string userId, string roleName);

    /// <summary>Снимает роль. KeyNotFound — нет пользователя/роли или роль не назначена.</summary>
    Task RemoveRoleAsync(string userId, string roleName);

    Task<List<string>> GetUserRolesAsync(string userId);
}
