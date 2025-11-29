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

        // 规则版本化
        public async Task<EventRuleRevision> CreateRuleRevisionAsync(EventRuleRevision rev)
        {
            // 并发安全：重试获取最大版本并写入，避免唯一索引冲突
            const int maxAttempts = 3;
            Exception? lastEx = null;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var maxVer = await _context.EventRuleRevisions
                    .Where(r => r.EventId == rev.EventId)
                    .MaxAsync(r => (int?)r.Version) ?? 0;
                rev.Version = maxVer + 1;
                _context.EventRuleRevisions.Add(rev);
                try
                {
                    await _context.SaveChangesAsync();
                    return rev;
                }
                catch (DbUpdateException ex)
                {
                    // 唯一索引冲突时等待并重试
                    lastEx = ex;
                    _context.Entry(rev).State = EntityState.Detached;
                    await Task.Delay(50);
                }
            }
            throw new InvalidOperationException("创建规则修订失败：并发冲突，请稍后重试。", lastEx);
        }

        public async Task<IEnumerable<EventRuleRevision>> GetRuleRevisionsAsync(Guid eventId)
        {
            return await _context.EventRuleRevisions
                .Where(r => r.EventId == eventId)
                .OrderByDescending(r => r.Version)
                .ToListAsync();
        }

        public async Task<bool> PublishRuleRevisionAsync(Guid eventId, Guid revisionId)
        {
            var rev = await _context.EventRuleRevisions.FirstOrDefaultAsync(r => r.Id == revisionId && r.EventId == eventId);
            var evt = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            if (rev == null || evt == null) return false;
            // 更新赛事当前规则
            evt.RulesMarkdown = rev.ContentMarkdown;
            evt.UpdatedAt = ChinaNow();
            // 标记发布
            rev.IsPublished = true;
            rev.PublishedAt = ChinaNow();
            await _context.SaveChangesAsync();
            return true;
        }

        // 报名表 Schema：写入 Event.CustomData.registrationFormSchema
        public async Task<bool> UpdateRegistrationFormSchemaAsync(Guid eventId, string schemaJson)
        {
            var evt = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            if (evt == null) return false;
            var current = string.IsNullOrWhiteSpace(evt.CustomData) ? "{}" : evt.CustomData;
            Dictionary<string, object?> dict;
            try { dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(current) ?? new(); }
            catch { dict = new(); }
            dict["registrationFormSchema"] = schemaJson;
            try
            {
                using var sd = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(schemaJson) ? "{}" : schemaJson);
                var root = sd.RootElement;
                if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (root.TryGetProperty("playerTypeRequirements", out var req))
                    {
                        dict["playerTypeRequirements"] = req.GetRawText();
                    }
                }
            }
            catch { }
            evt.CustomData = System.Text.Json.JsonSerializer.Serialize(dict);
            evt.UpdatedAt = ChinaNow();
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<EventRegistrationAnswer> UpsertRegistrationAnswersAsync(Guid eventId, Guid teamId, string answersJson, string? submittedByUserId)
        {
            var existing = await _context.EventRegistrationAnswers.FirstOrDefaultAsync(a => a.EventId == eventId && a.TeamId == teamId);
            if (existing == null)
            {
                var a = new EventRegistrationAnswer { EventId = eventId, TeamId = teamId, AnswersJson = answersJson, SubmittedByUserId = submittedByUserId, SubmittedAt = ChinaNow() };
                _context.EventRegistrationAnswers.Add(a);
                await _context.SaveChangesAsync();
                return a;
            }
            else
            {
                existing.AnswersJson = answersJson;
                existing.SubmittedByUserId = submittedByUserId;
                existing.UpdatedAt = ChinaNow();
                await _context.SaveChangesAsync();
                return existing;
            }
        }

        public async Task<EventRegistrationAnswer?> GetRegistrationAnswersAsync(Guid eventId, Guid teamId)
        {
            return await _context.EventRegistrationAnswers.FirstOrDefaultAsync(a => a.EventId == eventId && a.TeamId == teamId);
        }

        // Solo player registrations
        public async Task<PlayerEvent> RegisterPlayerToEventAsync(PlayerEvent playerEvent)
        {
            _context.PlayerEvents.Add(playerEvent);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("保存报名数据失败，请确认玩家与赛事存在且未重复报名。", ex);
            }
            return playerEvent;
        }

        public async Task<bool> UnregisterPlayerFromEventAsync(Guid playerId, Guid eventId)
        {
            var pe = await _context.PlayerEvents.FirstOrDefaultAsync(x => x.PlayerId == playerId && x.EventId == eventId);
            if (pe == null) return false;
            _context.PlayerEvents.Remove(pe);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsPlayerRegisteredAsync(Guid playerId, Guid eventId)
        {
            return await _context.PlayerEvents.AnyAsync(x => x.PlayerId == playerId && x.EventId == eventId);
        }

        public async Task<IEnumerable<PlayerEvent>> GetEventPlayerRegistrationsAsync(Guid eventId)
        {
            return await _context.PlayerEvents
                .Include(x => x.Player)
                .Include(x => x.Event)
                .Where(x => x.EventId == eventId)
                .OrderBy(x => x.RegistrationTime)
                .ToListAsync();
        }
    }
}
