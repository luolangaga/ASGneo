using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Repositories;

namespace ASG.Api.Services
{
    public class RoleService : IRoleService
    {
        private readonly IUserRepository _userRepository;

        public RoleService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<IEnumerable<RoleInfoDto>> GetAllRolesAsync()
        {
            var roles = Enum.GetValues<UserRole>().Select(role => new RoleInfoDto
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

            return MapToUserResponseDto(user);
        }

        public async Task<IEnumerable<UserResponseDto>> GetUsersByRoleAsync(UserRole role)
        {
            var users = await _userRepository.GetUsersByRoleAsync(role);
            return users.Select(MapToUserResponseDto);
        }

        public async Task<UserListDto> GetUsersWithPaginationAsync(int pageNumber, int pageSize)
        {
            var users = await _userRepository.GetUsersWithPaginationAsync(pageNumber, pageSize);
            var totalCount = await _userRepository.GetTotalUserCountAsync();
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
            
            foreach (UserRole role in Enum.GetValues<UserRole>())
            {
                var count = await _userRepository.GetUserCountByRoleAsync(role);
                statistics[role] = count;
            }

            return statistics;
        }

        public bool CanAssignRole(UserRole currentUserRole, UserRole targetRole)
        {
            // SuperAdmin 可以分配任何角色
            if (currentUserRole == UserRole.SuperAdmin)
                return true;

            // Admin 可以分配 User 角色，但不能分配 Admin 或 SuperAdmin 角色
            if (currentUserRole == UserRole.Admin)
                return targetRole == UserRole.User;

            // User 不能分配任何角色
            return false;
        }

        public bool HasPermission(UserRole userRole, string permission)
        {
            return userRole.HasPermission(permission);
        }

        private static UserResponseDto MapToUserResponseDto(User user)
        {
            return new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Role = user.Role,
                RoleDisplayName = user.RoleDisplayName,
                RoleName = user.RoleName,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                IsActive = user.IsActive
            };
        }
    }
}