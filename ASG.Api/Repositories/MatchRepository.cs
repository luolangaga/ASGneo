using ASG.Api.Data;
using ASG.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ASG.Api.Repositories
{
    /// <summary>
    /// 赛程数据访问实现
    /// </summary>
    public class MatchRepository : IMatchRepository
    {
        private readonly ApplicationDbContext _context;
        private static DateTime ChinaNow()
        {
            return DateTime.UtcNow;
        }

        public MatchRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Match>> GetAllMatchesAsync(Guid? eventId = null, int page = 1, int pageSize = 10, int? groupIndex = null, string? groupLabel = null)
        {
            var query = _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Event)
                .AsQueryable();

            if (eventId.HasValue)
            {
                query = query.Where(m => m.EventId == eventId.Value);
            }

            if (groupIndex.HasValue)
            {
                var needle = $"\"groupIndex\":{groupIndex.Value}";
                query = query.Where(m => m.CustomData != null && m.CustomData.Contains(needle));
            }
            if (!string.IsNullOrWhiteSpace(groupLabel))
            {
                var safe = groupLabel!.Replace("\"", string.Empty);
                var needle = $"\"groupLabel\":\"{safe}\"";
                query = query.Where(m => m.CustomData != null && m.CustomData.Contains(needle));
            }

            return await query
                .OrderByDescending(m => m.MatchTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetMatchCountAsync(Guid? eventId = null, int? groupIndex = null, string? groupLabel = null)
        {
            var query = _context.Matches.AsQueryable();

            if (eventId.HasValue)
            {
                query = query.Where(m => m.EventId == eventId.Value);
            }

            if (groupIndex.HasValue)
            {
                var needle = $"\"groupIndex\":{groupIndex.Value}";
                query = query.Where(m => m.CustomData != null && m.CustomData.Contains(needle));
            }
            if (!string.IsNullOrWhiteSpace(groupLabel))
            {
                var safe = groupLabel!.Replace("\"", string.Empty);
                var needle = $"\"groupLabel\":\"{safe}\"";
                query = query.Where(m => m.CustomData != null && m.CustomData.Contains(needle));
            }

            return await query.CountAsync();
        }

        public async Task<Match?> GetMatchByIdAsync(Guid id)
        {
            return await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Event)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<Match> CreateMatchAsync(Match match)
        {
            _context.Matches.Add(match);
            await _context.SaveChangesAsync();
            return match;
        }

        public async Task<Match> UpdateMatchAsync(Match match)
        {
            match.UpdatedAt = ChinaNow();
            _context.Matches.Update(match);
            await _context.SaveChangesAsync();
            return match;
        }

        public async Task<int> UpdateMatchesAsync(IEnumerable<Match> matches)
        {
            var list = matches?.ToList() ?? new List<Match>();
            if (list.Count == 0) return 0;
            var now = ChinaNow();
            foreach (var m in list) m.UpdatedAt = now;
            _context.Matches.UpdateRange(list);
            return await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteMatchAsync(Guid id)
        {
            var match = await _context.Matches.FindAsync(id);
            if (match == null) return false;

            _context.Matches.Remove(match);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> LikeMatchAsync(Guid id)
        {
            var match = await _context.Matches.FindAsync(id);
            if (match == null) throw new Exception("赛程未找到");

            match.Likes++;
            await _context.SaveChangesAsync();
            return match.Likes;
        }

        public async Task<IEnumerable<Match>> GetMatchesByIdsAsync(Guid eventId, IEnumerable<Guid> ids)
        {
            var set = (ids ?? Array.Empty<Guid>()).ToHashSet();
            if (set.Count == 0) return new List<Match>();
            return await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Event)
                .Where(m => m.EventId == eventId && set.Contains(m.Id))
                .ToListAsync();
        }
    }
}
