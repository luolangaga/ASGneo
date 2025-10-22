using ASG.Api.Models;

namespace ASG.Api.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(string id);
        Task<User?> GetByEmailAsync(string email);
        Task<IEnumerable<User>> GetAllAsync();
        Task<User> CreateAsync(User user);
        Task<User> UpdateAsync(User user);
        Task DeleteAsync(string id);
        Task<bool> ExistsAsync(string id);
        
        // 角色管理相关方法
        Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role);
        Task<bool> UpdateUserRoleAsync(string userId, UserRole role);
        Task<int> GetUserCountByRoleAsync(UserRole role);
        Task<IEnumerable<User>> GetUsersWithPaginationAsync(int pageNumber, int pageSize);
        Task<int> GetTotalUserCountAsync();
    }
}