using ASG.Api.Data;
using ASG.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ASG.Api.Repositories
{
    /// <summary>
    /// 赛事数据访问实现
    /// </summary>
    public class EventRepository : IEventRepository
    {
        private readonly ApplicationDbContext _context;
        private static DateTime ChinaNow()
        {
            return DateTime.UtcNow;
        }

        public EventRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Event>> GetAllEventsAsync()
        {
            return await _context.Events
                .Include(e => e.ChampionTeam)
                .Include(e => e.TeamEvents)
                    .ThenInclude(te => te.Team)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Event>> SearchEventsAsync(string query, int page = 1, int pageSize = 12)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 12;

            var q = (query ?? string.Empty).Trim();
            var eventsQuery = _context.Events
                .Include(e => e.ChampionTeam)
                .Include(e => e.TeamEvents)
                    .ThenInclude(te => te.Team)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                // 名称或描述模糊匹配
                eventsQuery = eventsQuery.Where(e =>
                    EF.Functions.Like(e.Name, "%" + q + "%") ||
                    (e.Description != null && EF.Functions.Like(e.Description, "%" + q + "%"))
                );
            }

            return await eventsQuery
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetSearchEventsCountAsync(string query)
        {
            var q = (query ?? string.Empty).Trim();
            var eventsQuery = _context.Events.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                eventsQuery = eventsQuery.Where(e =>
                    EF.Functions.Like(e.Name, "%" + q + "%") ||
                    (e.Description != null && EF.Functions.Like(e.Description, "%" + q + "%"))
                );
            }
            return await eventsQuery.CountAsync();
        }

        public async Task<Event?> GetEventByIdAsync(Guid id)
        {
            return await _context.Events
                .Include(e => e.ChampionTeam)
                .Include(e => e.TeamEvents)
                    .ThenInclude(te => te.Team)
                .Include(e => e.EventAdmins)
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<Event?> GetEventByNameAsync(string name)
        {
            return await _context.Events
                .FirstOrDefaultAsync(e => e.Name == name);
        }

        public async Task<Event> CreateEventAsync(Event eventEntity)
        {
            _context.Events.Add(eventEntity);
            await _context.SaveChangesAsync();
            return eventEntity;
        }

        public async Task<Event> UpdateEventAsync(Event eventEntity)
        {
            eventEntity.UpdatedAt = ChinaNow();
            _context.Events.Update(eventEntity);
            await _context.SaveChangesAsync();
            return eventEntity;
        }

        public async Task<bool> DeleteEventAsync(Guid id)
        {
            var eventEntity = await _context.Events.FindAsync(id);
            if (eventEntity == null)
                return false;

            _context.Events.Remove(eventEntity);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<TeamEvent>> GetEventRegistrationsAsync(Guid eventId)
        {
            return await _context.TeamEvents
                .Include(te => te.Team)
                .Include(te => te.Event)
                .Where(te => te.EventId == eventId)
                .OrderBy(te => te.RegistrationTime)
                .ToListAsync();
        }

        public async Task<TeamEvent> RegisterTeamToEventAsync(TeamEvent teamEvent)
        {
            _context.TeamEvents.Add(teamEvent);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // 将数据库更新异常转换为业务层可理解的异常，避免返回500
                throw new InvalidOperationException("保存报名数据失败，请确认战队与赛事存在且未重复报名。", ex);
            }
            return teamEvent;
        }

        public async Task<bool> UnregisterTeamFromEventAsync(Guid teamId, Guid eventId)
        {
            var teamEvent = await _context.TeamEvents
                .FirstOrDefaultAsync(te => te.TeamId == teamId && te.EventId == eventId);

            if (teamEvent == null)
                return false;

            _context.TeamEvents.Remove(teamEvent);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<TeamEvent?> UpdateTeamRegistrationStatusAsync(Guid teamId, Guid eventId, RegistrationStatus status, string? notes = null)
        {
            var teamEvent = await _context.TeamEvents
                .FirstOrDefaultAsync(te => te.TeamId == teamId && te.EventId == eventId);

            if (teamEvent == null)
                return null;

            teamEvent.Status = status;
            if (notes != null)
                teamEvent.Notes = notes;

            await _context.SaveChangesAsync();
            return teamEvent;
        }

        public async Task<bool> IsTeamRegisteredAsync(Guid teamId, Guid eventId)
        {
            return await _context.TeamEvents
                .AnyAsync(te => te.TeamId == teamId && te.EventId == eventId);
        }

        public async Task<IEnumerable<TeamEvent>> GetTeamRegistrationsAsync(Guid teamId)
        {
            return await _context.TeamEvents
                .Include(te => te.Event)
                .Include(te => te.Team)
                .Where(te => te.TeamId == teamId)
                .OrderByDescending(te => te.RegistrationTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Event>> GetEventsByCreatorAsync(string userId)
        {
            return await _context.Events
                .Include(e => e.TeamEvents)
                    .ThenInclude(te => te.Team)
                .Where(e => e.CreatedByUserId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Event>> GetActiveRegistrationEventsAsync()
        {
            var now = ChinaNow();
            return await _context.Events
                .Include(e => e.TeamEvents)
                    .ThenInclude(te => te.Team)
                .Include(e => e.EventAdmins)
                .Where(e => e.Status == EventStatus.RegistrationOpen && 
                           e.RegistrationStartTime <= now && 
                           e.RegistrationEndTime >= now)
                .OrderBy(e => e.RegistrationEndTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Event>> GetActiveRegistrationEventsAsync(int page, int pageSize)
        {
            var now = ChinaNow();
            return await _context.Events
                .Include(e => e.TeamEvents)
                    .ThenInclude(te => te.Team)
                .Include(e => e.EventAdmins)
                .Where(e => e.Status == EventStatus.RegistrationOpen &&
                            e.RegistrationStartTime <= now &&
                            e.RegistrationEndTime >= now)
                .OrderBy(e => e.RegistrationEndTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetActiveRegistrationEventsCountAsync()
        {
            var now = ChinaNow();
            return await _context.Events
                .Where(e => e.Status == EventStatus.RegistrationOpen &&
                            e.RegistrationStartTime <= now &&
                            e.RegistrationEndTime >= now)
                .CountAsync();
        }

        public async Task<IEnumerable<Event>> GetUpcomingEventsAsync()
        {
            var now = ChinaNow();
            return await _context.Events
                .Include(e => e.TeamEvents)
                    .ThenInclude(te => te.Team)
                .Include(e => e.EventAdmins)
                .Where(e => e.CompetitionStartTime > now)
                .OrderBy(e => e.CompetitionStartTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Event>> GetUpcomingEventsAsync(int page, int pageSize)
        {
            var now = ChinaNow();
            return await _context.Events
                .Include(e => e.TeamEvents)
                    .ThenInclude(te => te.Team)
                .Include(e => e.EventAdmins)
                .Where(e => e.CompetitionStartTime > now)
                .OrderBy(e => e.CompetitionStartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetUpcomingEventsCountAsync()
        {
            var now = ChinaNow();
            return await _context.Events
                .Where(e => e.CompetitionStartTime > now)
                .CountAsync();
        }

        public async Task<IEnumerable<Event>> GetChampionEventsByTeamAsync(Guid teamId)
        {
            return await _context.Events
                .Include(e => e.ChampionTeam)
                .Include(e => e.TeamEvents)
                .Include(e => e.EventAdmins)
                .Where(e => e.ChampionTeamId == teamId)
                .OrderByDescending(e => e.CompetitionEndTime ?? e.CompetitionStartTime)
                .ToListAsync();
        }

        public async Task<bool> AddEventAdminAsync(Guid eventId, string userId)
        {
            var exists = await _context.EventAdmins.AnyAsync(x => x.EventId == eventId && x.UserId == userId);
            if (exists) return true;
            _context.EventAdmins.Add(new EventAdmin { EventId = eventId, UserId = userId });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveEventAdminAsync(Guid eventId, string userId)
        {
            var admin = await _context.EventAdmins.FirstOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId);
            if (admin == null) return false;
            _context.EventAdmins.Remove(admin);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<string>> GetEventAdminUserIdsAsync(Guid eventId)
        {
            return await _context.EventAdmins
                .Where(x => x.EventId == eventId)
                .Select(x => x.UserId)
                .ToListAsync();
        }

        public async Task<bool> IsEventAdminAsync(Guid eventId, string userId)
        {
            return await _context.EventAdmins.AnyAsync(x => x.EventId == eventId && x.UserId == userId);
        }
    }
}