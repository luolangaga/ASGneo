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

        public async Task<IEnumerable<Team>> SearchTeamsByNameAsync(string name, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _context.Teams
                .Include(t => t.Players)
                .Include(t => t.Owner)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                // 使用 LIKE 以支持模糊匹配，并让数据库决定大小写规则
                query = query.Where(t => EF.Functions.Like(t.Name, "%" + name + "%"));
            }

            return await query
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
            // 仅更新战队本身的更新时间；依赖跟踪的实体状态进行保存
            // 注意：不要对整个实体图调用 Update，否则新添加的玩家会被错误地视为“修改”并触发并发异常
            team.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                // 并发修复策略：如果是 Player 处于 Modified 且数据库中已不存在该主键行，则改为 Added 并重试保存
                var fixedAny = false;
                foreach (var entry in ex.Entries)
                {
                    if (entry.Entity is Player player && entry.State == EntityState.Modified)
                    {
                        var exists = await _context.Players.AsNoTracking().AnyAsync(p => p.Id == player.Id);
                        if (!exists)
                        {
                            // 将丢失的玩家改为新增插入，避免并发更新失败
                            entry.State = EntityState.Added;
                            // 保证必要字段有效
                            if (player.Id == Guid.Empty)
                            {
                                player.Id = Guid.NewGuid();
                            }
                            player.TeamId = team.Id;
                            player.CreatedAt = DateTime.UtcNow;
                            player.UpdatedAt = DateTime.UtcNow;
                            fixedAny = true;
                        }
                    }
                }

                if (fixedAny)
                {
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // 无法自动修复，抛出更明确的业务异常以便前端提示
                    var details = string.Join("; ", ex.Entries.Select(e =>
                    {
                        var pkProps = e.Properties.Where(p => p.Metadata.IsPrimaryKey());
                        var pk = string.Join(", ", pkProps.Select(p => $"{p.Metadata.Name}={p.CurrentValue}"));
                        return $"Entity={e.Metadata.Name}, State={e.State}, PK=[{pk}]";
                    }));
                    throw new InvalidOperationException($"并发更新冲突：{details}. 可能的原因：要更新的玩家或战队在数据库中不存在，或被其他操作删除。", ex);
                }
            }

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

        public async Task<int> GetSearchTeamCountAsync(string name)
        {
            var query = _context.Teams.AsQueryable();
            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(t => EF.Functions.Like(t.Name, "%" + name + "%"));
            }
            return await query.CountAsync();
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