using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Repositories;
using Microsoft.AspNetCore.Identity;

namespace ASG.Api.Services
{
    public class RoleService : IRoleService
    {
        private readonly IUserRepository _userRepository;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public RoleService(IUserRepository userRepository, UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userRepository = userRepository;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IEnumerable<RoleInfoDto>> GetAllRolesAsync()
        {
            // 仅返回 User 与 Admin 两个角色（移除 SuperAdmin）
            var rolesEnum = new[] { UserRole.User, UserRole.Admin };
            var roles = rolesEnum.Select(role => new RoleInfoDto
            {
                Role = role,
                RoleName = role.GetRoleName(),
                DisplayName = role.GetDisplayName(),
                Value = (int)role
            });

            return await Task.FromResult(roles);
        }

        public async Task<UserResponseDto?> UpdateUserRoleAsync(UpdateUserRoleDto updateRoleDto)
        {
            var success = await _userRepository.UpdateUserRoleAsync(updateRoleDto.UserId, updateRoleDto.Role);
            if (!success)
                return null;

            var user = await _userRepository.GetByIdAsync(updateRoleDto.UserId);
            if (user == null)
                return null;

            var roleName = user.RoleName;
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }
            var currentRoles = await _userManager.GetRolesAsync(user);
            foreach (var r in currentRoles.Where(x => x == "User" || x == "Admin").ToList())
            {
                if (r != roleName)
                {
                    await _userManager.RemoveFromRoleAsync(user, r);
                }
            }
            if (!await _userManager.IsInRoleAsync(user, roleName))
            {
                await _userManager.AddToRoleAsync(user, roleName);
            }

            return MapToUserResponseDto(user);
        }

        public async Task<IEnumerable<UserResponseDto>> GetUsersByRoleAsync(UserRole role)
        {
            var users = await _userRepository.GetUsersByRoleAsync(role);
            return users.Select(MapToUserResponseDto);
        }

        public async Task<UserListDto> GetUsersWithPaginationAsync(int pageNumber, int pageSize, string? search = null)
        {
            var users = await _userRepository.GetUsersWithPaginationAsync(pageNumber, pageSize, search);
            var totalCount = await _userRepository.GetTotalUserCountAsync(search);
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            return new UserListDto
            {
                Users = users.Select(MapToUserResponseDto).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }

        public async Task<Dictionary<UserRole, int>> GetRoleStatisticsAsync()
        {
            var statistics = new Dictionary<UserRole, int>();
            // 仅统计 User 与 Admin 两个角色
            var rolesEnum = new[] { UserRole.User, UserRole.Admin };
            foreach (var role in rolesEnum)
            {
                var count = await _userRepository.GetUserCountByRoleAsync(role);
                statistics[role] = count;
            }
            return statistics;
        }

        public bool CanAssignRole(UserRole currentUserRole, UserRole targetRole)
        {
            // 历史兼容：SuperAdmin 等同于 Admin
            if (currentUserRole == UserRole.SuperAdmin)
                currentUserRole = UserRole.Admin;

            // Admin 可以分配 User 或 Admin 角色
            if (currentUserRole == UserRole.Admin)
                return targetRole == UserRole.User || targetRole == UserRole.Admin;

            // User 不能分配任何角色
            return false;
        }

        public bool HasPermission(UserRole userRole, string permission)
        {
            return userRole.HasPermission(permission);
        }

        private static UserResponseDto MapToUserResponseDto(User user)
        {
            // 统一将 SuperAdmin 视为 Admin（显示与数值）
            var normalizedRole = user.Role == UserRole.SuperAdmin ? UserRole.Admin : user.Role;
            return new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = normalizedRole,
                RoleDisplayName = normalizedRole.GetDisplayName(),
                RoleName = normalizedRole.GetRoleName(),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                IsActive = user.IsActive,
                TeamId = user.TeamId,
                EmailCredits = user.EmailCredits
            };
        }
    }
}
