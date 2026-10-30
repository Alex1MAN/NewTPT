using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Services;
using JWTAuthTemplate.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace JWTAuthTemplate.WebAPI.Controllers
{
    [ApiController]
    [Route("roles")]

    public class RolesController : BaseController
    {
        private readonly IRoleService _roleService;
        private readonly UserManager<ApplicationUser> _userManager;

        public RolesController(IRoleService roleService, UserManager<ApplicationUser> userManager)
        {
            _roleService = roleService;
            _userManager = userManager;
        }

        [HttpGet("get-all")]
        public async Task<ActionResult<IEnumerable<string>>> GetAllRoles()
        {
            var roles = await _roleService.GetAllRolesAsync();
            return Ok(roles);
        }

        [HttpPost("create-{roleName}")]
        public async Task<ActionResult> CreateRole(string roleName)
        {
            var success = await _roleService.CreateRoleAsync(roleName);
            if (success)
            {
                return CreatedAtAction(nameof(GetAllRoles), null);
            }
            return Conflict($"Role '{roleName}' already exists.");
        }

        [HttpDelete("delete-{roleName}")]
        public async Task<ActionResult> DeleteRole(string roleName)
        {
            var success = await _roleService.DeleteRoleAsync(roleName);
            if (success)
            {
                return NoContent();
            }
            return NotFound($"Role '{roleName}' does not exist.");
        }



        [HttpPut("assign-user-role/{userId}/{roleName}")]
        public async Task<ActionResult> AssignRoleToUserAsync(Guid userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return NotFound("User not found.");
            }

            bool roleExist = await _roleService.CheckRoleExist(roleName);
            if (!roleExist)
            {
                return NotFound("Role not found.");
            }

            bool roleAssignedBefore = await _roleService.CheckUserInRoleAsync(user, roleName);
            if (roleAssignedBefore)
            {
                return Conflict($"Role '{roleName}' assigned before.");
            }

            var success = await _roleService.AssignRoleToUserAsync(user, roleName);
            if (success)
            {
                return Ok(true);
            }
            return BadRequest($"Error in assigning role '{roleName}'.");
        }

        [HttpDelete("remove-user-role/{userId}/{roleName}")]
        public async Task<ActionResult> RemoveRoleFromUserAsync(Guid userId, string roleName)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return NotFound("User not found.");
            }

            bool roleExist = await _roleService.CheckRoleExist(roleName);
            if (!roleExist)
            {
                return NotFound("Role not found.");
            }

            bool roleAssignedBefore = await _roleService.CheckUserInRoleAsync(user, roleName);
            if (!roleAssignedBefore)
            {
                return Conflict($"Role '{roleName}' deleted before.");
            }

            var success = await _roleService.RemoveRoleFromUserAsync(user, roleName);
            if (success)
            {
                return Ok(true);
            }
            return BadRequest($"Error in deleting role '{roleName}'.");
        }

        [HttpGet("user-roles/{userId}")]
        public async Task<ActionResult<IEnumerable<string>>> GetUserRolesAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var roles = await _roleService.GetUserRolesAsync(user);
            return Ok(roles);
        }
    }
}
