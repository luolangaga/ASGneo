using ASG.Api.DTOs;
using ASG.Api.Models;

namespace ASG.Api.Services
{
    public interface IRoleService
    {
        Task<IEnumerable<RoleInfoDto>> GetAllRolesAsync();
        Task<UserResponseDto?> UpdateUserRoleAsync(UpdateUserRoleDto updateRoleDto);
        Task<IEnumerable<UserResponseDto>> GetUsersByRoleAsync(UserRole role);
        Task<UserListDto> GetUsersWithPaginationAsync(int pageNumber, int pageSize);
        Task<Dictionary<UserRole, int>> GetRoleStatisticsAsync();
        bool CanAssignRole(UserRole currentUserRole, UserRole targetRole);
        bool HasPermission(UserRole userRole, string permission);
    }
}