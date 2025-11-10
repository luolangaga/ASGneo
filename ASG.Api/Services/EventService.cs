using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Repositories;
using Microsoft.AspNetCore.Identity;
using System.Text;

namespace ASG.Api.Services
{
    /// <summary>
    /// 赛事业务逻辑实现
    /// </summary>
    public class EventService : IEventService
    {
        private readonly IEventRepository _eventRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly UserManager<User> _userManager;

        public EventService(IEventRepository eventRepository, ITeamRepository teamRepository, UserManager<User> userManager)
        {
            _eventRepository = eventRepository;
            _teamRepository = teamRepository;
            _userManager = userManager;
        }

        public async Task<IEnumerable<EventDto>> GetAllEventsAsync()
        {
            var events = await _eventRepository.GetAllEventsAsync();
            return events.Select(MapToEventDto);
        }

        public async Task<EventDto?> GetEventByIdAsync(Guid id)
        {
            var eventEntity = await _eventRepository.GetEventByIdAsync(id);
            return eventEntity != null ? MapToEventDto(eventEntity) : null;
        }

        public async Task<EventDto> CreateEventAsync(CreateEventDto createEventDto, string creatorUserId)
        {
            // 验证赛事名称是否已存在
            var existingEvent = await _eventRepository.GetEventByNameAsync(createEventDto.Name);
            if (existingEvent != null)
            {
                throw new InvalidOperationException("赛事名称已存在");
            }

            // 统一将时间标准化为 UTC，避免本地/未指定时区导致的比较误判
            var regStartUtc = EnsureUtc(createEventDto.RegistrationStartTime);
            var regEndUtc = EnsureUtc(createEventDto.RegistrationEndTime);
            var compStartUtc = EnsureUtc(createEventDto.CompetitionStartTime);
            var compEndUtc = createEventDto.CompetitionEndTime.HasValue 
                ? EnsureUtc(createEventDto.CompetitionEndTime.Value) 
                : (DateTime?)null;

            // 验证时间逻辑（使用归一化后的 UTC 时间）
            ValidateEventTimes(regStartUtc, regEndUtc, compStartUtc, compEndUtc);

            var eventEntity = new Event
            {
                Id = Guid.NewGuid(),
                Name = createEventDto.Name,
                Description = createEventDto.Description,
                RegistrationStartTime = regStartUtc,
                RegistrationEndTime = regEndUtc,
                CompetitionStartTime = compStartUtc,
                CompetitionEndTime = compEndUtc,
                MaxTeams = createEventDto.MaxTeams,
                Status = EventStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = creatorUserId
            };

            var createdEvent = await _eventRepository.CreateEventAsync(eventEntity);
            return MapToEventDto(createdEvent);
        }

        public async Task<EventDto?> UpdateEventAsync(Guid id, UpdateEventDto updateEventDto, string userId)
        {
            var eventEntity = await _eventRepository.GetEventByIdAsync(id);
            if (eventEntity == null)
                return null;

            // 验证权限
            if (!await CanUserManageEventAsync(id, userId))
            {
                throw new UnauthorizedAccessException("您没有权限修改此赛事");
            }

            // 验证赛事名称是否已存在（排除当前赛事）
            var existingEvent = await _eventRepository.GetEventByNameAsync(updateEventDto.Name);
            if (existingEvent != null && existingEvent.Id != id)
            {
                throw new InvalidOperationException("赛事名称已存在");
            }

            // 统一将时间标准化为 UTC
            var regStartUtc = EnsureUtc(updateEventDto.RegistrationStartTime);
            var regEndUtc = EnsureUtc(updateEventDto.RegistrationEndTime);
            var compStartUtc = EnsureUtc(updateEventDto.CompetitionStartTime);
            var compEndUtc = updateEventDto.CompetitionEndTime.HasValue 
                ? EnsureUtc(updateEventDto.CompetitionEndTime.Value) 
                : (DateTime?)null;

            // 验证时间逻辑（使用归一化后的 UTC 时间）
            ValidateEventTimes(regStartUtc, regEndUtc, compStartUtc, compEndUtc);

            // 更新赛事信息
            eventEntity.Name = updateEventDto.Name;
            eventEntity.Description = updateEventDto.Description;
            eventEntity.RegistrationStartTime = regStartUtc;
            eventEntity.RegistrationEndTime = regEndUtc;
            eventEntity.CompetitionStartTime = compStartUtc;
            eventEntity.CompetitionEndTime = compEndUtc;
            eventEntity.MaxTeams = updateEventDto.MaxTeams;
            eventEntity.Status = updateEventDto.Status;

            var updatedEvent = await _eventRepository.UpdateEventAsync(eventEntity);
            return MapToEventDto(updatedEvent);
        }

        public async Task<bool> DeleteEventAsync(Guid id, string userId)
        {
            var eventEntity = await _eventRepository.GetEventByIdAsync(id);
            if (eventEntity == null)
                return false;

            // 验证权限
            if (!await CanUserManageEventAsync(id, userId))
            {
                throw new UnauthorizedAccessException("您没有权限删除此赛事");
            }

            return await _eventRepository.DeleteEventAsync(id);
        }

        public async Task<TeamEventDto?> RegisterTeamToEventAsync(Guid eventId, RegisterTeamToEventDto registerDto, string userId)
        {
            var eventEntity = await _eventRepository.GetEventByIdAsync(eventId);
            if (eventEntity == null)
            {
                throw new InvalidOperationException("赛事不存在");
            }

            var team = await _teamRepository.GetTeamByIdAsync(registerDto.TeamId);
            if (team == null)
            {
                throw new InvalidOperationException("战队不存在");
            }

            // 验证用户是否有权限为该战队报名
            if (!await CanUserManageTeamRegistrationAsync(registerDto.TeamId, userId))
            {
                throw new UnauthorizedAccessException("您没有权限为此战队报名");
            }

            // 验证赛事状态和报名时间（统一用 UTC 进行比较）
            var now = DateTime.UtcNow;
            if (eventEntity.Status != EventStatus.RegistrationOpen)
            {
                throw new InvalidOperationException("赛事未开放报名");
            }

            var eventRegStartUtc = EnsureUtc(eventEntity.RegistrationStartTime);
            var eventRegEndUtc = EnsureUtc(eventEntity.RegistrationEndTime);

            if (now < eventRegStartUtc || now > eventRegEndUtc)
            {
                throw new InvalidOperationException("不在报名时间范围内");
            }

            // 验证是否已报名
            if (await _eventRepository.IsTeamRegisteredAsync(registerDto.TeamId, eventId))
            {
                throw new InvalidOperationException("战队已报名此赛事");
            }

            // 验证报名人数限制
            if (eventEntity.MaxTeams.HasValue)
            {
                var registrations = await _eventRepository.GetEventRegistrationsAsync(eventId);
                var approvedCount = registrations.Count(r => r.Status == RegistrationStatus.Approved);
                if (approvedCount >= eventEntity.MaxTeams.Value)
                {
                    throw new InvalidOperationException("报名人数已满");
                }
            }

            var teamEvent = new TeamEvent
            {
                TeamId = registerDto.TeamId,
                EventId = eventId,
                RegistrationTime = DateTime.UtcNow,
                Status = RegistrationStatus.Pending,
                Notes = registerDto.Notes,
                RegisteredByUserId = userId
            };

            var createdTeamEvent = await _eventRepository.RegisterTeamToEventAsync(teamEvent);
            return MapToTeamEventDto(createdTeamEvent, team.Name, eventEntity.Name);
        }

        public async Task<bool> UnregisterTeamFromEventAsync(Guid eventId, Guid teamId, string userId)
        {
            // 验证用户权限
            if (!await CanUserManageTeamRegistrationAsync(teamId, userId) && 
                !await CanUserManageEventAsync(eventId, userId))
            {
                throw new UnauthorizedAccessException("您没有权限取消此报名");
            }

            return await _eventRepository.UnregisterTeamFromEventAsync(teamId, eventId);
        }

        public async Task<TeamEventDto?> UpdateTeamRegistrationStatusAsync(Guid eventId, Guid teamId, UpdateTeamRegistrationDto updateDto, string userId)
        {
            // 验证用户权限（只有赛事创建者可以更新报名状态）
            if (!await CanUserManageEventAsync(eventId, userId))
            {
                throw new UnauthorizedAccessException("您没有权限修改报名状态");
            }

            var updatedTeamEvent = await _eventRepository.UpdateTeamRegistrationStatusAsync(teamId, eventId, updateDto.Status, updateDto.Notes);
            if (updatedTeamEvent == null)
                return null;

            var team = await _teamRepository.GetTeamByIdAsync(teamId);
            var eventEntity = await _eventRepository.GetEventByIdAsync(eventId);

            return MapToTeamEventDto(updatedTeamEvent, team?.Name ?? "", eventEntity?.Name ?? "");
        }

        public async Task<IEnumerable<TeamEventDto>> GetEventRegistrationsAsync(Guid eventId)
        {
            var registrations = await _eventRepository.GetEventRegistrationsAsync(eventId);
            return registrations.Select(te => MapToTeamEventDto(te, te.Team?.Name ?? "", te.Event?.Name ?? ""));
        }

        public async Task<IEnumerable<TeamEventDto>> GetTeamRegistrationsAsync(Guid teamId)
        {
            var registrations = await _eventRepository.GetTeamRegistrationsAsync(teamId);
            return registrations.Select(te => MapToTeamEventDto(te, te.Team?.Name ?? "", te.Event?.Name ?? ""));
        }

        public async Task<IEnumerable<EventDto>> GetEventsByCreatorAsync(string userId)
        {
            var events = await _eventRepository.GetEventsByCreatorAsync(userId);
            return events.Select(MapToEventDto);
        }

        public async Task<IEnumerable<EventDto>> GetActiveRegistrationEventsAsync()
        {
            var events = await _eventRepository.GetActiveRegistrationEventsAsync();
            return events.Select(MapToEventDto);
        }

        public async Task<PagedResult<EventDto>> GetActiveRegistrationEventsAsync(int page = 1, int pageSize = 12)
        {
            var events = await _eventRepository.GetActiveRegistrationEventsAsync(page, pageSize);
            var totalCount = await _eventRepository.GetActiveRegistrationEventsCountAsync();
            return new PagedResult<EventDto>
            {
                Items = events.Select(MapToEventDto),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<IEnumerable<EventDto>> GetUpcomingEventsAsync()
        {
            var events = await _eventRepository.GetUpcomingEventsAsync();
            return events.Select(MapToEventDto);
        }

        public async Task<PagedResult<EventDto>> GetUpcomingEventsAsync(int page = 1, int pageSize = 12)
        {
            var events = await _eventRepository.GetUpcomingEventsAsync(page, pageSize);
            var totalCount = await _eventRepository.GetUpcomingEventsCountAsync();
            return new PagedResult<EventDto>
            {
                Items = events.Select(MapToEventDto),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<IEnumerable<EventDto>> GetChampionEventsByTeamAsync(Guid teamId)
        {
            var events = await _eventRepository.GetChampionEventsByTeamAsync(teamId);
            return events.Select(MapToEventDto);
        }

        public async Task<EventDto?> SetChampionTeamAsync(Guid eventId, SetChampionDto dto, string userId)
        {
            var eventEntity = await _eventRepository.GetEventByIdAsync(eventId);
            if (eventEntity == null)
                return null;

            // 权限校验：赛事创建者或管理员
            if (!await CanUserManageEventAsync(eventId, userId))
                throw new UnauthorizedAccessException("您没有权限设置该赛事的冠军");

            // 允许清除冠军（TeamId=null）
            if (dto.TeamId.HasValue)
            {
                var team = await _teamRepository.GetTeamByIdAsync(dto.TeamId.Value);
                if (team == null)
                    throw new InvalidOperationException("战队不存在");

                // 可选校验：仅允许设置为已报名并通过审核的队伍
                var registrations = await _eventRepository.GetEventRegistrationsAsync(eventId);
                var approvedTeamIds = registrations
                    .Where(r => r.Status == RegistrationStatus.Approved)
                    .Select(r => r.TeamId)
                    .ToHashSet();

                if (!approvedTeamIds.Contains(dto.TeamId.Value))
                    throw new InvalidOperationException("只能设置为已批准参赛的战队");

                eventEntity.ChampionTeamId = dto.TeamId.Value;
            }
            else
            {
                eventEntity.ChampionTeamId = null;
            }

            eventEntity.UpdatedAt = DateTime.UtcNow;
            var updated = await _eventRepository.UpdateEventAsync(eventEntity);
            return MapToEventDto(updated);
        }

        public async Task<bool> CanUserManageEventAsync(Guid eventId, string userId)
        {
            var eventEntity = await _eventRepository.GetEventByIdAsync(eventId);
            if (eventEntity == null)
                return false;

            // 赛事创建者可以管理
            if (eventEntity.CreatedByUserId == userId)
                return true;

            // 管理员可以管理
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && await _userManager.IsInRoleAsync(user, "Administrator"))
                return true;

            return false;
        }

        public async Task<bool> CanUserManageTeamRegistrationAsync(Guid teamId, string userId)
        {
            var team = await _teamRepository.GetTeamByIdAsync(teamId);
            if (team == null)
                return false;

            // 战队拥有者可以管理
            if (team.OwnerId == userId)
                return true;

            // 战队成员可以管理
            var players = await _teamRepository.GetTeamPlayersAsync(teamId);
            if (players.Any(p => p.UserId == userId))
                return true;

            return false;
        }

        public async Task<byte[]> ExportEventRegistrationsCsvAsync(Guid eventId, string userId)
        {
            // 权限校验：赛事创建者或管理员
            if (!await CanUserManageEventAsync(eventId, userId))
            {
                throw new UnauthorizedAccessException("您没有权限导出此赛事的报名信息");
            }

            var eventEntity = await _eventRepository.GetEventByIdAsync(eventId);
            if (eventEntity == null)
            {
                throw new InvalidOperationException("赛事不存在");
            }

            var registrations = await _eventRepository.GetEventRegistrationsAsync(eventId);

            var sb = new StringBuilder();
            // 表头
            sb.AppendLine(string.Join(",",
                new[]
                {
                    "EventName","EventId","TeamName","TeamId","RegistrationStatus","RegistrationTime","Notes",
                    "PlayerId","PlayerName","GameId","GameRank","PlayerDescription"
                }));

            foreach (var te in registrations)
            {
                var teamName = te.Team?.Name ?? string.Empty;
                var players = await _teamRepository.GetTeamPlayersAsync(te.TeamId);

                if (players == null || !players.Any())
                {
                    // 没有队员时也输出一行，队员字段为空
                    sb.AppendLine(string.Join(",",
                        new[]
                        {
                            CsvEscape(eventEntity.Name), CsvEscape(eventEntity.Id.ToString()), CsvEscape(teamName), CsvEscape(te.TeamId.ToString()),
                            CsvEscape(te.Status.ToString()), CsvEscape(te.RegistrationTime.ToString("yyyy-MM-ddTHH:mm:ssZ")), CsvEscape(te.Notes ?? string.Empty),
                            CsvEscape(string.Empty), CsvEscape(string.Empty), CsvEscape(string.Empty), CsvEscape(string.Empty), CsvEscape(string.Empty)
                        }));
                }
                else
                {
                    foreach (var p in players)
                    {
                        sb.AppendLine(string.Join(",",
                            new[]
                            {
                                CsvEscape(eventEntity.Name), CsvEscape(eventEntity.Id.ToString()), CsvEscape(teamName), CsvEscape(te.TeamId.ToString()),
                                CsvEscape(te.Status.ToString()), CsvEscape(te.RegistrationTime.ToString("yyyy-MM-ddTHH:mm:ssZ")), CsvEscape(te.Notes ?? string.Empty),
                                CsvEscape(p.Id.ToString()), CsvEscape(p.Name ?? string.Empty), CsvEscape(p.GameId ?? string.Empty), CsvEscape(p.GameRank ?? string.Empty), CsvEscape(p.Description ?? string.Empty)
                            }));
                    }
                }
            }

            // 使用UTF-8 BOM以利于Excel正确识别中文
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return encoding.GetBytes(sb.ToString());
        }

        private static string CsvEscape(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            var escaped = s.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        /// <summary>
        /// 将 DateTime 统一转换为 UTC。
        /// - 若为 Utc，直接返回；
        /// - 若为 Local，转换为 Utc；
        /// - 若为 Unspecified，按本地时间解释后转换为 Utc（避免遗漏时区信息导致的误判）。
        /// </summary>
        private static DateTime EnsureUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc) return dt;
            if (dt.Kind == DateTimeKind.Local) return dt.ToUniversalTime();
            // 未指定时区：按本地时间解释，再转为 UTC
            var assumedLocal = DateTime.SpecifyKind(dt, DateTimeKind.Local);
            return assumedLocal.ToUniversalTime();
        }

        private static void ValidateEventTimes(DateTime registrationStart, DateTime registrationEnd,
                                               DateTime competitionStart, DateTime? competitionEnd)
        {
            if (registrationStart >= registrationEnd)
            {
                throw new ArgumentException("报名开始时间必须早于报名结束时间");
            }

            if (registrationEnd >= competitionStart)
            {
                throw new ArgumentException("报名结束时间必须早于比赛开始时间");
            }

            if (competitionEnd.HasValue && competitionStart >= competitionEnd.Value)
            {
                throw new ArgumentException("比赛开始时间必须早于比赛结束时间");
            }
        }

        private static EventDto MapToEventDto(Event eventEntity)
        {
            return new EventDto
            {
                Id = eventEntity.Id,
                Name = eventEntity.Name,
                Description = eventEntity.Description,
                RegistrationStartTime = eventEntity.RegistrationStartTime,
                RegistrationEndTime = eventEntity.RegistrationEndTime,
                CompetitionStartTime = eventEntity.CompetitionStartTime,
                CompetitionEndTime = eventEntity.CompetitionEndTime,
                MaxTeams = eventEntity.MaxTeams,
                Status = eventEntity.Status,
                CreatedAt = eventEntity.CreatedAt,
                UpdatedAt = eventEntity.UpdatedAt,
                CreatedByUserId = eventEntity.CreatedByUserId,
                ChampionTeamId = eventEntity.ChampionTeamId,
                ChampionTeamName = eventEntity.ChampionTeam?.Name,
                RegisteredTeamsCount = eventEntity.TeamEvents?.Count ?? 0,
                RegisteredTeams = eventEntity.TeamEvents?.Select(te => MapToTeamEventDto(te, te.Team?.Name ?? "", eventEntity.Name)).ToList()
            };
        }

        private static TeamEventDto MapToTeamEventDto(TeamEvent teamEvent, string teamName, string eventName)
        {
            return new TeamEventDto
            {
                TeamId = teamEvent.TeamId,
                EventId = teamEvent.EventId,
                TeamName = teamName,
                EventName = eventName,
                RegistrationTime = teamEvent.RegistrationTime,
                Status = teamEvent.Status,
                Notes = teamEvent.Notes,
                RegisteredByUserId = teamEvent.RegisteredByUserId
            };
        }
    }
}