using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Repositories;
using Microsoft.AspNetCore.Identity;

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

            // 验证时间逻辑
            ValidateEventTimes(createEventDto.RegistrationStartTime, createEventDto.RegistrationEndTime,
                               createEventDto.CompetitionStartTime, createEventDto.CompetitionEndTime);

            var eventEntity = new Event
            {
                Id = Guid.NewGuid(),
                Name = createEventDto.Name,
                Description = createEventDto.Description,
                RegistrationStartTime = createEventDto.RegistrationStartTime.UtcDateTime,
                RegistrationEndTime = createEventDto.RegistrationEndTime.UtcDateTime,
                CompetitionStartTime = createEventDto.CompetitionStartTime.UtcDateTime,
                CompetitionEndTime = createEventDto.CompetitionEndTime?.UtcDateTime,
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

            // 验证时间逻辑
            ValidateEventTimes(updateEventDto.RegistrationStartTime, updateEventDto.RegistrationEndTime,
                               updateEventDto.CompetitionStartTime, updateEventDto.CompetitionEndTime);

            // 更新赛事信息
            eventEntity.Name = updateEventDto.Name;
            eventEntity.Description = updateEventDto.Description;
            eventEntity.RegistrationStartTime = updateEventDto.RegistrationStartTime.UtcDateTime;
            eventEntity.RegistrationEndTime = updateEventDto.RegistrationEndTime.UtcDateTime;
            eventEntity.CompetitionStartTime = updateEventDto.CompetitionStartTime.UtcDateTime;
            eventEntity.CompetitionEndTime = updateEventDto.CompetitionEndTime?.UtcDateTime;
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
                throw new InvalidOperationException("团队不存在");
            }

            // 验证用户是否有权限为该团队报名
            if (!await CanUserManageTeamRegistrationAsync(registerDto.TeamId, userId))
            {
                throw new UnauthorizedAccessException("您没有权限为此团队报名");
            }

            // 验证赛事状态和报名时间
            var now = DateTime.UtcNow;
            if (eventEntity.Status != EventStatus.RegistrationOpen)
            {
                throw new InvalidOperationException("赛事未开放报名");
            }

            if (now < eventEntity.RegistrationStartTime || now > eventEntity.RegistrationEndTime)
            {
                throw new InvalidOperationException("不在报名时间范围内");
            }

            // 验证是否已报名
            if (await _eventRepository.IsTeamRegisteredAsync(registerDto.TeamId, eventId))
            {
                throw new InvalidOperationException("团队已报名此赛事");
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

        public async Task<IEnumerable<EventDto>> GetUpcomingEventsAsync()
        {
            var events = await _eventRepository.GetUpcomingEventsAsync();
            return events.Select(MapToEventDto);
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

            // 团队拥有者可以管理
            if (team.OwnerId == userId)
                return true;

            // 团队成员可以管理
            var players = await _teamRepository.GetTeamPlayersAsync(teamId);
            if (players.Any(p => p.UserId == userId))
                return true;

            return false;
        }

        private static void ValidateEventTimes(DateTimeOffset registrationStart, DateTimeOffset registrationEnd,
                                               DateTimeOffset competitionStart, DateTimeOffset? competitionEnd)
        {
            if (registrationStart.UtcDateTime >= registrationEnd.UtcDateTime)
            {
                throw new ArgumentException("报名开始时间必须早于报名结束时间");
            }

            if (registrationEnd.UtcDateTime >= competitionStart.UtcDateTime)
            {
                throw new ArgumentException("报名结束时间必须早于比赛开始时间");
            }

            if (competitionEnd.HasValue && competitionStart.UtcDateTime >= competitionEnd.Value.UtcDateTime)
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
                RegistrationStartTime = new DateTimeOffset(DateTime.SpecifyKind(eventEntity.RegistrationStartTime, DateTimeKind.Utc)),
                RegistrationEndTime = new DateTimeOffset(DateTime.SpecifyKind(eventEntity.RegistrationEndTime, DateTimeKind.Utc)),
                CompetitionStartTime = new DateTimeOffset(DateTime.SpecifyKind(eventEntity.CompetitionStartTime, DateTimeKind.Utc)),
                CompetitionEndTime = eventEntity.CompetitionEndTime.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(eventEntity.CompetitionEndTime.Value, DateTimeKind.Utc)) : null,
                MaxTeams = eventEntity.MaxTeams,
                Status = eventEntity.Status,
                CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(eventEntity.CreatedAt, DateTimeKind.Utc)),
                UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(eventEntity.UpdatedAt, DateTimeKind.Utc)),
                CreatedByUserId = eventEntity.CreatedByUserId,
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
                RegistrationTime = new DateTimeOffset(DateTime.SpecifyKind(teamEvent.RegistrationTime, DateTimeKind.Utc)),
                Status = teamEvent.Status,
                Notes = teamEvent.Notes,
                RegisteredByUserId = teamEvent.RegisteredByUserId
            };
        }
    }
}