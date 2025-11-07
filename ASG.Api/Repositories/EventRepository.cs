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

        public EventRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Event>> GetAllEventsAsync()
        {
            return await _context.Events
                .Include(e => e.TeamEvents)
                    .ThenInclude(te => te.Team)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
        }

        public async Task<Event?> GetEventByIdAsync(Guid id)
        {
            return await _context.Events
                .Include(e => e.TeamEvents)
                    .ThenInclude(te => te.Team)
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
            eventEntity.UpdatedAt = DateTime.UtcNow;
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
            await _context.SaveChangesAsync();
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
            var now = DateTime.UtcNow;
            return await _context.Events
                .Include(e => e.TeamEvents)
                    .ThenInclude(te => te.Team)
                .Where(e => e.Status == EventStatus.RegistrationOpen && 
                           e.RegistrationStartTime <= now && 
                           e.RegistrationEndTime >= now)
                .OrderBy(e => e.RegistrationEndTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Event>> GetUpcomingEventsAsync()
        {
            var now = DateTime.UtcNow;
            return await _context.Events
                .Include(e => e.TeamEvents)
                    .ThenInclude(te => te.Team)
                .Where(e => e.CompetitionStartTime > now)
                .OrderBy(e => e.CompetitionStartTime)
                .ToListAsync();
        }
    }
}