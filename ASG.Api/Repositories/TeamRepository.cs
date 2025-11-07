using ASG.Api.Data;
using ASG.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ASG.Api.Repositories
{
    public class TeamRepository : ITeamRepository
    {
        private readonly ApplicationDbContext _context;

        public TeamRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Team>> GetAllTeamsAsync(int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            return await _context.Teams
                .Include(t => t.Players)
                .Include(t => t.Owner)
                .OrderBy(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<Team?> GetTeamByIdAsync(Guid id)
        {
            return await _context.Teams
                .Include(t => t.Owner)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<Team?> GetTeamByIdWithPlayersAsync(Guid id)
        {
            return await _context.Teams
                .Include(t => t.Players)
                .Include(t => t.Owner)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<Team?> GetTeamByNameAsync(string name)
        {
            return await _context.Teams
                .Include(t => t.Players)
                .Include(t => t.Owner)
                .FirstOrDefaultAsync(t => t.Name == name);
        }

        public async Task<Team> CreateTeamAsync(Team team)
        {
            team.CreatedAt = DateTime.UtcNow;
            team.UpdatedAt = DateTime.UtcNow;

            // 设置玩家的创建时间
            foreach (var player in team.Players)
            {
                player.CreatedAt = DateTime.UtcNow;
                player.UpdatedAt = DateTime.UtcNow;
                player.TeamId = team.Id;
            }

            _context.Teams.Add(team);
            await _context.SaveChangesAsync();

            return await GetTeamByIdWithPlayersAsync(team.Id) ?? team;
        }

        public async Task<Team> UpdateTeamAsync(Team team)
        {
            team.UpdatedAt = DateTime.UtcNow;

            _context.Teams.Update(team);
            await _context.SaveChangesAsync();

            return await GetTeamByIdWithPlayersAsync(team.Id) ?? team;
        }

        public async Task<bool> DeleteTeamAsync(Guid id)
        {
            var team = await _context.Teams.FindAsync(id);
            if (team == null)
                return false;

            _context.Teams.Remove(team);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> VerifyTeamPasswordAsync(Guid teamId, string password)
        {
            var team = await _context.Teams.FindAsync(teamId);
            return team != null && BCrypt.Net.BCrypt.Verify(password, team.Password);
        }

        public async Task<bool> TeamExistsAsync(Guid id)
        {
            return await _context.Teams.AnyAsync(t => t.Id == id);
        }

        public async Task<bool> TeamNameExistsAsync(string name, Guid? excludeId = null)
        {
            var query = _context.Teams.Where(t => t.Name == name);
            if (excludeId.HasValue)
            {
                query = query.Where(t => t.Id != excludeId.Value);
            }
            return await query.AnyAsync();
        }

        public async Task<IEnumerable<Player>> GetTeamPlayersAsync(Guid teamId)
        {
            return await _context.Players
                .Where(p => p.TeamId == teamId)
                .ToListAsync();
        }

        public async Task<int> GetTeamCountAsync()
        {
            return await _context.Teams.CountAsync();
        }

        public async Task<int> LikeTeamAsync(Guid id)
        {
            var team = await _context.Teams.FindAsync(id);
            if (team == null)
                throw new Exception("Team not found");

            team.Likes++;
            await _context.SaveChangesAsync();
            return team.Likes;
        }

    }
}