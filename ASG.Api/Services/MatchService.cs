using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Repositories;
using ASG.Api.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        private readonly IUserRepository _userRepository;

        public MatchService(IMatchRepository matchRepository, IEventRepository eventRepository, ITeamRepository teamRepository, IUserRepository userRepository)
        {
            _matchRepository = matchRepository;
            _eventRepository = eventRepository;
            _teamRepository = teamRepository;
            _userRepository = userRepository;
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

            // 权限校验：赛事创建者或管理员
            if (!await CanUserManageEventAsync(createDto.EventId, userId))
                throw new UnauthorizedAccessException("您没有权限为该赛事创建赛程");

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
                MatchTime = createDto.MatchTime,
                EventId = createDto.EventId,
                LiveLink = createDto.LiveLink,
                CustomData = createDto.CustomData,
                Commentator = createDto.Commentator,
                Director = createDto.Director,
                Referee = createDto.Referee,
                CreatedAt = ChinaNow(),
                UpdatedAt = ChinaNow()
            };

            var createdMatch = await _matchRepository.CreateMatchAsync(match);
            return MapToMatchDto(createdMatch);
        }

        public async Task<MatchDto?> UpdateMatchAsync(Guid id, UpdateMatchDto updateDto, string userId)
        {
            var match = await _matchRepository.GetMatchByIdAsync(id);
            if (match == null)
                return null;

            // 权限校验：赛事创建者或管理员
            if (!await CanUserManageEventAsync(match.EventId, userId))
                throw new UnauthorizedAccessException("您没有权限修改此赛程");

            // 更新字段
            if (updateDto.MatchTime.HasValue) match.MatchTime = updateDto.MatchTime.Value;
            if (updateDto.LiveLink != null) match.LiveLink = updateDto.LiveLink;
            if (updateDto.CustomData != null) match.CustomData = updateDto.CustomData;
            if (updateDto.Commentator != null) match.Commentator = updateDto.Commentator;
            if (updateDto.Director != null) match.Director = updateDto.Director;
            if (updateDto.Referee != null) match.Referee = updateDto.Referee;

            var updatedMatch = await _matchRepository.UpdateMatchAsync(match);
            return MapToMatchDto(updatedMatch);
        }

        public async Task<MatchDto?> UpdateMatchScoresAsync(Guid id, UpdateMatchScoresDto scoresDto, string userId)
        {
            var match = await _matchRepository.GetMatchByIdAsync(id);
            if (match == null)
                return null;

            if (!await CanUserManageEventAsync(match.EventId, userId))
                throw new UnauthorizedAccessException("您没有权限修改此赛程");

            var node = (!string.IsNullOrWhiteSpace(match.CustomData) ? JsonNode.Parse(match.CustomData) as JsonObject : null) ?? new JsonObject();

            var cleanedGames = new List<GameScoreDto>();
            if (scoresDto.Games != null)
            {
                foreach (var g in scoresDto.Games)
                {
                    var home = g != null ? Math.Max(0, g.Home) : 0;
                    var away = g != null ? Math.Max(0, g.Away) : 0;
                    cleanedGames.Add(new GameScoreDto { Home = home, Away = away });
                }
            }

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            node["games"] = JsonSerializer.SerializeToNode(cleanedGames, jsonOptions);

            var bo = scoresDto.BestOf.HasValue ? scoresDto.BestOf.Value : cleanedGames.Count;
            if (bo > 0) node["bestOf"] = bo; else node.Remove("bestOf");

            // 计算胜者并保存到自定义字段
            try
            {
                var homeWins = cleanedGames.Count(x => (x?.Home ?? 0) > (x?.Away ?? 0));
                var awayWins = cleanedGames.Count(x => (x?.Away ?? 0) > (x?.Home ?? 0));

                // 按 BestOf 判定需要的胜场数；若未设置则以更多胜场为胜
                var needed = bo > 0 ? ((bo + 1) / 2) : 0;
                Guid? winnerId = null;
                string? winnerName = null;
                if (needed > 0)
                {
                    if (homeWins >= needed)
                    {
                        winnerId = match.HomeTeamId;
                        winnerName = match.HomeTeam?.Name;
                    }
                    else if (awayWins >= needed)
                    {
                        winnerId = match.AwayTeamId;
                        winnerName = match.AwayTeam?.Name;
                    }
                }
                else
                {
                    if (homeWins > awayWins)
                    {
                        winnerId = match.HomeTeamId;
                        winnerName = match.HomeTeam?.Name;
                    }
                    else if (awayWins > homeWins)
                    {
                        winnerId = match.AwayTeamId;
                        winnerName = match.AwayTeam?.Name;
                    }
                }

                if (winnerId.HasValue)
                {
                    node["winnerTeamId"] = winnerId.Value.ToString();
                    if (!string.IsNullOrWhiteSpace(winnerName)) node["winnerTeamName"] = winnerName; else node.Remove("winnerTeamName");
                }
                else
                {
                    node.Remove("winnerTeamId");
                    node.Remove("winnerTeamName");
                }
            }
            catch
            {
                // 若解析或计算失败则不影响其它字段更新
            }

            match.CustomData = node.ToJsonString();

            var updated = await _matchRepository.UpdateMatchAsync(match);
            return MapToMatchDto(updated);
        }

        public async Task<bool> DeleteMatchAsync(Guid id, string userId)
        {
            var match = await _matchRepository.GetMatchByIdAsync(id);
            if (match == null)
                return false;

            // 权限校验：赛事创建者或管理员
            if (!await CanUserManageEventAsync(match.EventId, userId))
                throw new UnauthorizedAccessException("您没有权限删除此赛程");

            return await _matchRepository.DeleteMatchAsync(id);
        }

        public async Task<int> LikeMatchAsync(Guid id)
        {
            return await _matchRepository.LikeMatchAsync(id);
        }

        private MatchDto MapToMatchDto(Match match)
        {
            var dto = new MatchDto
            {
                Id = match.Id,
                HomeTeamId = match.HomeTeamId,
                HomeTeamName = match.HomeTeam?.Name ?? string.Empty,
                AwayTeamId = match.AwayTeamId,
                AwayTeamName = match.AwayTeam?.Name ?? string.Empty,
                MatchTime = match.MatchTime,
                EventId = match.EventId,
                EventName = match.Event?.Name ?? string.Empty,
                LiveLink = match.LiveLink,
                CustomData = match.CustomData,
                Commentator = match.Commentator,
                Director = match.Director,
                Referee = match.Referee,
                Likes = match.Likes,
                CreatedAt = match.CreatedAt,
                UpdatedAt = match.UpdatedAt
            };

            try
            {
                if (!string.IsNullOrWhiteSpace(match.CustomData))
                {
                    using var doc = JsonDocument.Parse(match.CustomData);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("stage", out var stageEl) && stageEl.ValueKind == JsonValueKind.String)
                        dto.Stage = stageEl.GetString();
                    if (root.TryGetProperty("winnerTeamId", out var wtiEl))
                    {
                        if (wtiEl.ValueKind == JsonValueKind.String)
                        {
                            var s = wtiEl.GetString();
                            if (!string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out var gid)) dto.WinnerTeamId = gid;
                        }
                        else if (wtiEl.ValueKind == JsonValueKind.Number)
                        {
                            var s = wtiEl.GetRawText();
                            if (Guid.TryParse(s, out var gid)) dto.WinnerTeamId = gid;
                        }
                    }
                    if (root.TryGetProperty("winnerTeamName", out var wtnEl) && wtnEl.ValueKind == JsonValueKind.String)
                        dto.WinnerTeamName = wtnEl.GetString();
                    if (root.TryGetProperty("replayLink", out var rlEl) && rlEl.ValueKind == JsonValueKind.String)
                        dto.ReplayLink = rlEl.GetString();
                }
                if (dto.WinnerTeamId.HasValue && string.IsNullOrEmpty(dto.WinnerTeamName))
                {
                    if (dto.WinnerTeamId.Value == match.HomeTeamId) dto.WinnerTeamName = match.HomeTeam?.Name;
                    else if (dto.WinnerTeamId.Value == match.AwayTeamId) dto.WinnerTeamName = match.AwayTeam?.Name;
                }
            }
            catch
            {
            }

            return dto;
        }

        private async Task<bool> CanUserManageEventAsync(Guid eventId, string userId)
        {
            var eventEntity = await _eventRepository.GetEventByIdAsync(eventId);
            if (eventEntity == null)
                return false;

            // 赛事创建者可以管理
            if (eventEntity.CreatedByUserId == userId)
                return true;

            // 管理员可以管理
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null && (user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin))
                return true;

            if (eventEntity.EventAdmins != null && eventEntity.EventAdmins.Any(a => a.UserId == userId))
                return true;

            return false;
        }

        private static DateTime ChinaNow()
        {
            return DateTime.UtcNow;
        }
    }
}
