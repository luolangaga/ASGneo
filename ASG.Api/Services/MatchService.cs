using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Repositories;
using ASG.Api.Services;

namespace ASG.Api.Services
{
    /// <summary>
    /// 赛程业务逻辑实现
    /// </summary>
    public class MatchService : IMatchService
    {
        private readonly IMatchRepository _matchRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ITeamRepository _teamRepository;

        public MatchService(IMatchRepository matchRepository, IEventRepository eventRepository, ITeamRepository teamRepository)
        {
            _matchRepository = matchRepository;
            _eventRepository = eventRepository;
            _teamRepository = teamRepository;
        }

        public async Task<IEnumerable<MatchDto>> GetAllMatchesAsync(Guid? eventId = null, int page = 1, int pageSize = 10)
        {
            var matches = await _matchRepository.GetAllMatchesAsync(eventId, page, pageSize);
            return matches.Select(m => MapToMatchDto(m));
        }

        public async Task<int> GetMatchCountAsync(Guid? eventId = null)
        {
            return await _matchRepository.GetMatchCountAsync(eventId);
        }

        public async Task<MatchDto?> GetMatchByIdAsync(Guid id)
        {
            var match = await _matchRepository.GetMatchByIdAsync(id);
            return match != null ? MapToMatchDto(match) : null;
        }

        public async Task<MatchDto> CreateMatchAsync(CreateMatchDto createDto, string userId)
        {
            // 验证赛事存在
            var eventEntity = await _eventRepository.GetEventByIdAsync(createDto.EventId);
            if (eventEntity == null)
                throw new InvalidOperationException("赛事不存在");

            // 验证主客队存在
            var homeTeam = await _teamRepository.GetTeamByIdAsync(createDto.HomeTeamId);
            if (homeTeam == null)
                throw new InvalidOperationException("主队不存在");

            var awayTeam = await _teamRepository.GetTeamByIdAsync(createDto.AwayTeamId);
            if (awayTeam == null)
                throw new InvalidOperationException("客队不存在");

            // 简单验证：主客队不能相同
            if (createDto.HomeTeamId == createDto.AwayTeamId)
                throw new InvalidOperationException("主客队不能相同");

            var match = new Match
            {
                HomeTeamId = createDto.HomeTeamId,
                AwayTeamId = createDto.AwayTeamId,
                MatchTime = createDto.MatchTime.UtcDateTime,
                EventId = createDto.EventId,
                LiveLink = createDto.LiveLink,
                CustomData = createDto.CustomData,
                Commentator = createDto.Commentator,
                Director = createDto.Director,
                Referee = createDto.Referee,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdMatch = await _matchRepository.CreateMatchAsync(match);
            return MapToMatchDto(createdMatch);
        }

        public async Task<MatchDto?> UpdateMatchAsync(Guid id, UpdateMatchDto updateDto, string userId)
        {
            var match = await _matchRepository.GetMatchByIdAsync(id);
            if (match == null)
                return null;

            // 更新字段
            if (updateDto.MatchTime.HasValue) match.MatchTime = updateDto.MatchTime.Value.UtcDateTime;
            if (updateDto.LiveLink != null) match.LiveLink = updateDto.LiveLink;
            if (updateDto.CustomData != null) match.CustomData = updateDto.CustomData;
            if (updateDto.Commentator != null) match.Commentator = updateDto.Commentator;
            if (updateDto.Director != null) match.Director = updateDto.Director;
            if (updateDto.Referee != null) match.Referee = updateDto.Referee;

            var updatedMatch = await _matchRepository.UpdateMatchAsync(match);
            return MapToMatchDto(updatedMatch);
        }

        public async Task<bool> DeleteMatchAsync(Guid id, string userId)
        {
            return await _matchRepository.DeleteMatchAsync(id);
        }

        public async Task<int> LikeMatchAsync(Guid id)
        {
            return await _matchRepository.LikeMatchAsync(id);
        }

        private MatchDto MapToMatchDto(Match match)
        {
            return new MatchDto
            {
                Id = match.Id,
                HomeTeamId = match.HomeTeamId,
                HomeTeamName = match.HomeTeam?.Name ?? string.Empty,
                AwayTeamId = match.AwayTeamId,
                AwayTeamName = match.AwayTeam?.Name ?? string.Empty,
                MatchTime = new DateTimeOffset(DateTime.SpecifyKind(match.MatchTime, DateTimeKind.Utc)),
                EventId = match.EventId,
                EventName = match.Event?.Name ?? string.Empty,
                LiveLink = match.LiveLink,
                CustomData = match.CustomData,
                Commentator = match.Commentator,
                Director = match.Director,
                Referee = match.Referee,
                Likes = match.Likes,
                CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(match.CreatedAt, DateTimeKind.Utc)),
                UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(match.UpdatedAt, DateTimeKind.Utc))
            };
        }
    }
}