using Microsoft.EntityFrameworkCore;
using ASG.Api.Data;
using ASG.Api.Models;

namespace ASG.Api.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByIdAsync(string id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _context.Users.Where(u => u.IsActive).ToListAsync();
        }

        public async Task<User> CreateAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> UpdateAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task DeleteAsync(string id)
        {
            var user = await GetByIdAsync(id);
            if (user != null)
            {
                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(string id)
        {
            return await _context.Users.AnyAsync(u => u.Id == id && u.IsActive);
        }

        // 角色管理相关方法实现
        public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role)
        {
            return await _context.Users
                .Where(u => u.IsActive && u.Role == role)
                .OrderBy(u => u.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> UpdateUserRoleAsync(string userId, UserRole role)
        {
            var user = await GetByIdAsync(userId);
            if (user == null || !user.IsActive)
                return false;

            user.Role = role;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetUserCountByRoleAsync(UserRole role)
        {
            return await _context.Users
                .CountAsync(u => u.IsActive && u.Role == role);
        }

        public async Task<IEnumerable<User>> GetUsersWithPaginationAsync(int pageNumber, int pageSize)
        {
            return await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalUserCountAsync()
        {
            return await _context.Users.CountAsync(u => u.IsActive);
        }

        public async Task<bool> UpdateEmailCreditsAsync(string userId, int credits)
        {
            var user = await GetByIdAsync(userId);
            if (user == null || !user.IsActive)
                return false;

            user.EmailCredits = Math.Max(0, credits);
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AdjustEmailCreditsAsync(string userId, int delta)
        {
            var user = await GetByIdAsync(userId);
            if (user == null || !user.IsActive)
                return false;

            var newCredits = user.EmailCredits + delta;
            user.EmailCredits = newCredits < 0 ? 0 : newCredits;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<User>> SearchByNameAsync(string name, int limit = 10)
        {
            name ??= string.Empty;
            var q = name.Trim();
            var query = _context.Users.AsQueryable();
            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(u => (u.FirstName + u.LastName).Contains(q) || u.UserName.Contains(q));
            }
            return await query.Where(u => u.IsActive)
                .OrderBy(u => u.CreatedAt)
                .Take(limit <= 0 ? 10 : limit)
                .ToListAsync();
        }
    }
}