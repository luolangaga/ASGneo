using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Services;
using ASG.Api.Authorization;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        /// <summary>
        /// 获取所有可用角色信息
        /// </summary>
        [HttpGet("roles")]
        public async Task<ActionResult<IEnumerable<RoleInfoDto>>> GetAllRoles()
        {
            var roles = await _roleService.GetAllRolesAsync();
            return Ok(roles);
        }

        /// <summary>
        /// 更新用户角色（仅管理员可操作）
        /// </summary>
        [HttpPut("update-role")]
        [Authorize(Policy = AuthorizationPolicies.CanAssignRoles)]
        public async Task<ActionResult<UserResponseDto>> UpdateUserRole([FromBody] UpdateUserRoleDto updateRoleDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 获取当前用户角色
            var currentUserRoleStr = User.FindFirst(ClaimTypes.Role)?.Value;
            if (!Enum.TryParse<UserRole>(currentUserRoleStr, out var currentUserRole))
                return StatusCode(403, new { message = "无法确定当前用户角色" });

            // 检查是否有权限分配目标角色
            if (!_roleService.CanAssignRole(currentUserRole, updateRoleDto.Role))
                return StatusCode(403, new { message = "您没有权限分配此角色" });

            var result = await _roleService.UpdateUserRoleAsync(updateRoleDto);
            if (result == null)
                return NotFound("用户不存在或更新失败");

            return Ok(result);
        }

        /// <summary>
        /// 根据角色获取用户列表（仅管理员可查看）
        /// </summary>
        [HttpGet("users-by-role/{role}")]
        [Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsersByRole(UserRole role)
        {
            var users = await _roleService.GetUsersByRoleAsync(role);
            return Ok(users);
        }

        /// <summary>
        /// 分页获取用户列表（仅管理员可查看）
        /// </summary>
        [HttpGet("users")]
        [Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
        public async Task<ActionResult<UserListDto>> GetUsersWithPagination(
            [FromQuery] int pageNumber = 1, 
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var result = await _roleService.GetUsersWithPaginationAsync(pageNumber, pageSize, search);
            return Ok(result);
        }

        /// <summary>
        /// 获取角色统计信息（管理员可查看）
        /// </summary>
        [HttpGet("statistics")]
        [Authorize(Policy = AuthorizationPolicies.RequireAdminRole)]
        public async Task<ActionResult<Dictionary<UserRole, int>>> GetRoleStatistics()
        {
            var statistics = await _roleService.GetRoleStatisticsAsync();
            return Ok(statistics);
        }

        /// <summary>
        /// 检查当前用户是否有特定权限
        /// </summary>
        [HttpGet("check-permission/{permission}")]
        public ActionResult<bool> CheckPermission(string permission)
        {
            var currentUserRoleStr = User.FindFirst(ClaimTypes.Role)?.Value;
            if (!Enum.TryParse<UserRole>(currentUserRoleStr, out var currentUserRole))
                return BadRequest("无法确定当前用户角色");

            var hasPermission = _roleService.HasPermission(currentUserRole, permission);
            return Ok(hasPermission);
        }

        /// <summary>
        /// 获取当前用户的角色信息
        /// </summary>
        [HttpGet("my-role")]
        public ActionResult<RoleInfoDto> GetMyRole()
        {
            var currentUserRoleStr = User.FindFirst(ClaimTypes.Role)?.Value;
            if (!Enum.TryParse<UserRole>(currentUserRoleStr, out var currentUserRole))
                return BadRequest("无法确定当前用户角色");

            // 历史兼容：将 SuperAdmin 视为 Admin
            if (currentUserRole == UserRole.SuperAdmin)
                currentUserRole = UserRole.Admin;

            var roleInfo = new RoleInfoDto
            {
                Role = currentUserRole,
                RoleName = currentUserRole.GetRoleName(),
                DisplayName = currentUserRole.GetDisplayName(),
                Value = (int)currentUserRole
            };

            return Ok(roleInfo);
        }
    }
}
