using ASG.Api.Data;
using ASG.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            return await _context.Users.ToListAsync();
        }

        public async Task<User> CreateAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task DeleteAsync(string id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(string id)
        {
            return await _context.Users.AnyAsync(u => u.Id == id);
        }

        public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role)
        {
            return await _context.Users.Where(u => u.Role == role).ToListAsync();
        }

        public async Task<bool> UpdateUserRoleAsync(string userId, UserRole role)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return false;
            }

            user.Role = role;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetUserCountByRoleAsync(UserRole role)
        {
            return await _context.Users.Where(u => u.Role == role && u.IsActive).CountAsync();
        }

        public async Task<bool> UpdateEmailCreditsAsync(string userId, int credits)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !user.IsActive)
            {
                return false;
            }

            user.EmailCredits = credits < 0 ? 0 : credits;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AdjustEmailCreditsAsync(string userId, int delta)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !user.IsActive)
            {
                return false;
            }

            var newCredits = user.EmailCredits + delta;
            if (newCredits < 0) newCredits = 0;
            user.EmailCredits = newCredits;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<User>> SearchByNameAsync(string name, int limit = 10)
        {
            var q = (name ?? string.Empty).Trim();
            var query = _context.Users.Where(u => u.IsActive);
            if (!string.IsNullOrEmpty(q))
            {
                var pattern = $"%{q}%";
                query = query.Where(u =>
                    EF.Functions.Like(u.Email, pattern) ||
                    EF.Functions.Like(u.FirstName + " " + u.LastName, pattern)
                );
            }

            return await query
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<IEnumerable<User>> GetUsersWithPaginationAsync(int pageNumber, int pageSize, string? search = null)
        {
            var query = _context.Users.Where(u => u.IsActive);
            
            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim();
                var pattern = $"%{q}%";
                query = query.Where(u => 
                    EF.Functions.Like(u.Email, pattern) || 
                    EF.Functions.Like(u.FirstName + " " + u.LastName, pattern)
                );
            }

            return await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalUserCountAsync(string? search = null)
        {
            var query = _context.Users.Where(u => u.IsActive);
            
            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim();
                var pattern = $"%{q}%";
                query = query.Where(u => 
                    EF.Functions.Like(u.Email, pattern) || 
                    EF.Functions.Like(u.FirstName + " " + u.LastName, pattern)
                );
            }

            return await query.CountAsync();
        }
    }
}
