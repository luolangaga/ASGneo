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

        public async Task<IEnumerable<MatchDto>> GetAllMatchesAsync(Guid? eventId = null, int page = 1, int pageSize = 10, int? groupIndex = null, string? groupLabel = null)
        {
            var matches = await _matchRepository.GetAllMatchesAsync(eventId, page, pageSize, groupIndex, groupLabel);
            return matches.Select(m => MapToMatchDto(m));
        }

        public async Task<int> GetMatchCountAsync(Guid? eventId = null, int? groupIndex = null, string? groupLabel = null)
        {
            return await _matchRepository.GetMatchCountAsync(eventId, groupIndex, groupLabel);
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
            try
            {
                if (!string.IsNullOrWhiteSpace(updatedMatch.CustomData))
                {
                    using var doc = JsonDocument.Parse(updatedMatch.CustomData);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("stage", out var s) && s.ValueKind == JsonValueKind.String)
                    {
                        var sv = (s.GetString() ?? string.Empty).Trim().ToLowerInvariant();
                        if (sv == "groups")
                        {
                            await RecalculateGroupStandingsAsync(updatedMatch.EventId);
                        }
                    }
                }
            }
            catch { }
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
            try
            {
                if (!string.IsNullOrWhiteSpace(updated.CustomData))
                {
                    using var doc = JsonDocument.Parse(updated.CustomData);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("stage", out var s) && s.ValueKind == JsonValueKind.String)
                    {
                        var sv = (s.GetString() ?? string.Empty).Trim().ToLowerInvariant();
                        if (sv == "groups")
                        {
                            await RecalculateGroupStandingsAsync(updated.EventId);
                        }
                    }
                }
            }
            catch { }
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

        public async Task<IEnumerable<MatchDto>> GenerateScheduleAsync(Guid eventId, GenerateScheduleRequestDto dto, string userId)
        {
            var eventEntity = await _eventRepository.GetEventByIdAsync(eventId);
            if (eventEntity == null) throw new InvalidOperationException("赛事不存在");
            if (!await CanUserManageEventAsync(eventId, userId)) throw new UnauthorizedAccessException("无权限");

            var regs = await _eventRepository.GetEventRegistrationsAsync(eventId);
            var teams = regs.Where(r => r.Status == RegistrationStatus.Approved || r.Status == RegistrationStatus.Confirmed).Select(r => r.Team!).Where(t => t != null).ToList();
            if (teams.Count < 2) throw new InvalidOperationException("参赛队伍不足");

            var interval = dto.IntervalMinutes ?? 60;
            var bestOf = dto.BestOf ?? 1;
            var now = ChinaNow();

            var created = new List<MatchDto>();

            var existingCount = await _matchRepository.GetMatchCountAsync(eventId);
            var existing = await _matchRepository.GetAllMatchesAsync(eventId, 1, Math.Max(1, existingCount));
            var teamNextTime = new Dictionary<Guid, DateTime>();
            foreach (var m in existing)
            {
                var t = m.MatchTime;
                if (!teamNextTime.ContainsKey(m.HomeTeamId) || teamNextTime[m.HomeTeamId] < t)
                    teamNextTime[m.HomeTeamId] = t.AddMinutes(interval);
                if (!teamNextTime.ContainsKey(m.AwayTeamId) || teamNextTime[m.AwayTeamId] < t)
                    teamNextTime[m.AwayTeamId] = t.AddMinutes(interval);
            }

            var configNode = (!string.IsNullOrWhiteSpace(eventEntity.CustomData) ? JsonNode.Parse(eventEntity.CustomData) as JsonObject : null) ?? new JsonObject();
            List<Guid> seeding = teams.Select(t => t.Id).ToList();
            try
            {
                if (configNode.TryGetPropertyValue("tournamentConfig", out var tc) && tc is JsonObject tcObj)
                {
                    if (tcObj.TryGetPropertyValue("seeding", out var seedNode) && seedNode is JsonArray arr)
                    {
                        var list = new List<Guid>();
                        foreach (var el in arr)
                        {
                            var s = el?.GetValue<string>();
                            if (!string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out var g)) list.Add(g);
                        }
                        if (list.Count >= 2) seeding = list;
                    }
                }
            }
            catch { }

            string NormalizeStage(string? s)
            {
                var v = (s ?? string.Empty).Trim().ToLowerInvariant();
                if (v == "se" || v == "single_elim") return "single_elim";
                if (v == "de" || v == "double_elim") return "double_elim";
                if (v == "swiss") return "swiss";
                if (v == "groups") return "groups";
                return string.Empty;
            }

            List<(Guid, Guid)> pairs = new List<(Guid, Guid)>();
            var pairGroupIndices = new List<int>();
            var stage = NormalizeStage(dto.Stage);

            if (stage == "single_elim" || stage == "double_elim")
            {
                var list = seeding.Where(id => teams.Any(t => t.Id == id)).ToList();
                if (list.Count % 2 == 1) list = list.Take(list.Count - 1).ToList();
                for (int i = 0; i + 1 < list.Count; i += 2)
                    pairs.Add((list[i], list[i + 1]));
            }
            else if (stage == "groups")
            {
                var groups = new List<List<Guid>>();
                int groupSizeFromConfig = 0;
                try
                {
                    if (configNode.TryGetPropertyValue("tournamentConfig", out var tc) && tc is JsonObject tcObj)
                    {
                        if (tcObj.TryGetPropertyValue("groups", out var gNode) && gNode is JsonArray gArr)
                        {
                            foreach (var grp in gArr)
                            {
                                var glist = new List<Guid>();
                                if (grp is JsonArray inner)
                                {
                                    foreach (var el in inner)
                                    {
                                        var s = el?.GetValue<string>();
                                        if (!string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out var gid)) glist.Add(gid);
                                    }
                                }
                                if (glist.Count >= 2) groups.Add(glist);
                            }
                        }
                        if (tcObj.TryGetPropertyValue("groupSize", out var gs) && gs is JsonNode gsNode)
                        {
                            try { groupSizeFromConfig = gsNode.GetValue<int>(); }
                            catch
                            {
                                var s = gsNode.GetValue<string?>();
                                if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out var gi)) groupSizeFromConfig = gi;
                            }
                        }
                    }
                }
                catch { }

                if (groups.Count == 0)
                {
                    var list = seeding.Where(id => teams.Any(t => t.Id == id)).ToList();
                    var gs = groupSizeFromConfig > 1 ? groupSizeFromConfig : 4;
                    for (int i = 0; i < list.Count; i += gs)
                    {
                        var glist = list.Skip(i).Take(gs).ToList();
                        if (glist.Count >= 2) groups.Add(glist);
                    }
                }

                var teamNameMap = teams.ToDictionary(t => t.Id, t => t.Name ?? string.Empty);
                var groupsMeta = new JsonArray();
                for (int gi = 0; gi < groups.Count; gi++)
                {
                    var g = groups[gi];
                    var label = gi < 26 ? ((char)('A' + gi)).ToString() : ($"G{gi + 1}");
                    var idsArr = new JsonArray(g.Select(x => (JsonNode)x.ToString()).ToArray());
                    var namesArr = new JsonArray(g.Select(x => (JsonNode)(teamNameMap.ContainsKey(x) ? teamNameMap[x] : string.Empty)).ToArray());
                    var obj = new JsonObject { ["index"] = gi, ["label"] = label, ["teamIds"] = idsArr, ["teamNames"] = namesArr };
                    groupsMeta.Add(obj);
                }
                configNode["groupStage"] = new JsonObject { ["groups"] = groupsMeta };
                eventEntity.CustomData = configNode.ToJsonString();
                await _eventRepository.UpdateEventAsync(eventEntity);

                foreach (var g in groups)
                {
                    for (int i = 0; i < g.Count; i++)
                    {
                        for (int j = i + 1; j < g.Count; j++)
                        {
                            pairs.Add((g[i], g[j]));
                            pairGroupIndices.Add(groups.IndexOf(g));
                        }
                    }
                }
            }
            else if (stage == "swiss")
            {
                var round = dto.Round ?? 1;
                var list = seeding.Where(id => teams.Any(t => t.Id == id)).ToList();
                if (round <= 1)
                {
                    if (list.Count % 2 == 1) list = list.Take(list.Count - 1).ToList();
                    for (int i = 0; i + 1 < list.Count; i += 2)
                        pairs.Add((list[i], list[i + 1]));
                }
                else
                {
                    var existingCount2 = await _matchRepository.GetMatchCountAsync(eventId);
                    var existing2 = await _matchRepository.GetAllMatchesAsync(eventId, 1, Math.Max(1, existingCount2));
                    var stageMatches = existing2.Where(m =>
                    {
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(m.CustomData))
                            {
                                using var doc = JsonDocument.Parse(m.CustomData);
                                var root = doc.RootElement;
                                if (root.TryGetProperty("stage", out var s) && s.ValueKind == JsonValueKind.String)
                                {
                                    var sv = (s.GetString() ?? string.Empty).Trim().ToLowerInvariant();
                                    return sv == "swiss";
                                }
                            }
                        }
                        catch { }
                        return false;
                    }).OrderBy(m => m.MatchTime).ToList();

                    var wins = new Dictionary<Guid, int>();
                    foreach (var id in list) wins[id] = 0;
                    foreach (var m in stageMatches)
                    {
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(m.CustomData))
                            {
                                using var doc = JsonDocument.Parse(m.CustomData);
                                var root = doc.RootElement;
                                if (root.TryGetProperty("winnerTeamId", out var w))
                                {
                                    Guid gid;
                                    if (w.ValueKind == JsonValueKind.String)
                                    {
                                        var s = w.GetString();
                                        if (!string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out gid)) if (wins.ContainsKey(gid)) wins[gid]++;
                                    }
                                    else if (w.ValueKind == JsonValueKind.Number)
                                    {
                                        var s = w.GetRawText();
                                        if (Guid.TryParse(s, out gid)) if (wins.ContainsKey(gid)) wins[gid]++;
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    var prevPairs = new HashSet<string>();
                    foreach (var m in stageMatches)
                    {
                        var a = m.HomeTeamId;
                        var b = m.AwayTeamId;
                        var key = a.CompareTo(b) < 0 ? ($"{a}-{b}") : ($"{b}-{a}");
                        prevPairs.Add(key);
                    }

                    var groups = new Dictionary<int, List<Guid>>();
                    foreach (var id in list)
                    {
                        var w = wins.ContainsKey(id) ? wins[id] : 0;
                        if (!groups.ContainsKey(w)) groups[w] = new List<Guid>();
                        groups[w].Add(id);
                    }
                    var levels = groups.Keys.OrderByDescending(x => x).ToList();
                    foreach (var lvl in levels)
                        groups[lvl] = groups[lvl].OrderBy(x => x).ToList();

                    for (int i = 0; i < levels.Count - 1; i++)
                    {
                        var cur = levels[i];
                        var nxt = levels[i + 1];
                        var arr = groups[cur];
                        if (arr.Count % 2 == 1)
                        {
                            var moved = arr[arr.Count - 1];
                            arr.RemoveAt(arr.Count - 1);
                            groups[nxt].Add(moved);
                        }
                    }

                    foreach (var lvl in levels)
                    {
                        var arr = groups[lvl];
                        var used = new HashSet<Guid>();
                        for (int i = 0; i < arr.Count; i++)
                        {
                            var a = arr[i];
                            if (used.Contains(a)) continue;
                            Guid? picked = null;
                            for (int j = i + 1; j < arr.Count; j++)
                            {
                                var b = arr[j];
                                if (used.Contains(b)) continue;
                                var k = a.CompareTo(b) < 0 ? ($"{a}-{b}") : ($"{b}-{a}");
                                if (!prevPairs.Contains(k)) { picked = b; break; }
                            }
                            if (!picked.HasValue)
                            {
                                for (int j = i + 1; j < arr.Count; j++)
                                {
                                    var b = arr[j];
                                    if (used.Contains(b)) { continue; }
                                    picked = b; break;
                                }
                            }
                            if (picked.HasValue)
                            {
                                pairs.Add((a, picked.Value));
                                used.Add(a);
                                used.Add(picked.Value);
                            }
                        }
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("未知赛制");
            }

            var startDate = dto.StartDate.HasValue ? dto.StartDate.Value.Date : eventEntity.CompetitionStartTime.Date;
            TimeSpan dailyStart = new TimeSpan(eventEntity.CompetitionStartTime.Hour, eventEntity.CompetitionStartTime.Minute, 0);
            if (!string.IsNullOrWhiteSpace(dto.DailyStartTime))
            {
                try
                {
                    var parts = dto.DailyStartTime.Split(':');
                    var hh = int.Parse(parts[0]);
                    var mm = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                    dailyStart = new TimeSpan(hh, mm, 0);
                }
                catch { }
            }
            var maxPerDay = dto.MaxMatchesPerDay.HasValue && dto.MaxMatchesPerDay.Value > 0 ? dto.MaxMatchesPerDay.Value : int.MaxValue;
            var currentDay = startDate;
            var dayCount = 0;
            var currentSlot = ChinaLocalToUtc(new DateTime(currentDay.Year, currentDay.Month, currentDay.Day, dailyStart.Hours, dailyStart.Minutes, 0, DateTimeKind.Unspecified));

            DateTime BaseStart()
            {
                return ChinaLocalToUtc(new DateTime(startDate.Year, startDate.Month, startDate.Day, dailyStart.Hours, dailyStart.Minutes, 0, DateTimeKind.Unspecified));
            }
            DateTime NextAvailable(Guid a, Guid b)
            {
                var baseStart = BaseStart();
                var tA = teamNextTime.ContainsKey(a) ? teamNextTime[a] : baseStart;
                var tB = teamNextTime.ContainsKey(b) ? teamNextTime[b] : baseStart;
                var t = new DateTime(Math.Max(tA.Ticks, tB.Ticks), DateTimeKind.Utc);
                return t;
            }

            for (int idx = 0; idx < pairs.Count; idx++)
            {
                var a = pairs[idx].Item1;
                var b = pairs[idx].Item2;
                var gi = pairGroupIndices.Count > idx ? pairGroupIndices[idx] : -1;
                var label = gi >= 0 ? (gi < 26 ? ((char)('A' + gi)).ToString() : ($"G{gi + 1}")) : string.Empty;
                var t = NextAvailable(a, b);
                var candidate = currentSlot;
                var scheduled = new DateTime(Math.Max(candidate.Ticks, t.Ticks), DateTimeKind.Utc);

                var m = new Match
                {
                    HomeTeamId = a,
                    AwayTeamId = b,
                    MatchTime = scheduled,
                    EventId = eventId,
                    CustomData = new JsonObject
                    {
                        ["stage"] = stage,
                        ["bestOf"] = bestOf,
                        ["groupIndex"] = gi >= 0 ? gi : JsonValue.Create((int?)null),
                        ["groupLabel"] = string.IsNullOrEmpty(label) ? JsonValue.Create((string?)null) : label,
                        ["bracketRound"] = (stage == "single_elim" || stage == "double_elim") ? 1 : (stage == "swiss" ? (dto.Round ?? 1) : JsonValue.Create((int?)null)),
                        ["bracketOrder"] = (stage == "single_elim" || stage == "double_elim" || stage == "swiss") ? (idx + 1) : JsonValue.Create((int?)null)
                    }.ToJsonString(),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                var createdMatch = await _matchRepository.CreateMatchAsync(m);
                created.Add(MapToMatchDto(createdMatch));

                teamNextTime[a] = scheduled.AddMinutes(interval);
                teamNextTime[b] = scheduled.AddMinutes(interval);

                dayCount++;
                if (dayCount >= maxPerDay)
                {
                    currentDay = currentDay.AddDays(1);
                    dayCount = 0;
                    currentSlot = ChinaLocalToUtc(new DateTime(currentDay.Year, currentDay.Month, currentDay.Day, dailyStart.Hours, dailyStart.Minutes, 0, DateTimeKind.Unspecified));
                }
                else
                {
                    currentSlot = scheduled.AddMinutes(interval);
                }
            }

            return created;
        }

        public async Task<IEnumerable<MatchDto>> GenerateNextRoundAsync(Guid eventId, GenerateNextRoundRequestDto dto, string userId)
        {
            var eventEntity = await _eventRepository.GetEventByIdAsync(eventId);
            if (eventEntity == null) throw new InvalidOperationException("赛事不存在");
            if (!await CanUserManageEventAsync(eventId, userId)) throw new UnauthorizedAccessException("无权限");

            var count = await _matchRepository.GetMatchCountAsync(eventId);
            var existing = await _matchRepository.GetAllMatchesAsync(eventId, 1, Math.Max(1, count));
            string NormalizeStage(string? s)
            {
                var v = (s ?? string.Empty).Trim().ToLowerInvariant();
                if (v == "se" || v == "single_elim") return "single_elim";
                if (v == "de" || v == "double_elim") return "double_elim";
                if (v == "swiss") return "swiss";
                if (v == "groups") return "groups";
                return v;
            }
            var stage = NormalizeStage(dto.Stage);
            var interval = dto.IntervalMinutes ?? 60;
            var bestOf = dto.BestOf ?? 1;

            var stageMatches = existing.Where(m =>
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(m.CustomData))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(m.CustomData);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("stage", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var sv = s.GetString()?.Trim().ToLowerInvariant();
                            return sv == stage;
                        }
                    }
                }
                catch { }
                return false;
            }).OrderBy(m => m.MatchTime).ToList();
            if (stage == "groups")
            {
                var configNode = (!string.IsNullOrWhiteSpace(eventEntity.CustomData) ? System.Text.Json.Nodes.JsonNode.Parse(eventEntity.CustomData) as System.Text.Json.Nodes.JsonObject : null) ?? new System.Text.Json.Nodes.JsonObject();
                int advancePerGroup = 2;
                try
                {
                    if (configNode.TryGetPropertyValue("tournamentConfig", out var tc) && tc is System.Text.Json.Nodes.JsonObject tcObj)
                    {
                        if (tcObj.TryGetPropertyValue("advancePerGroup", out var apg) && apg is System.Text.Json.Nodes.JsonNode apgNode)
                        {
                            try { advancePerGroup = apgNode.GetValue<int>(); }
                            catch { var s = apgNode.GetValue<string?>(); if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out var v)) advancePerGroup = v; }
                        }
                    }
                }
                catch { }

                var groupTeams = new Dictionary<int, HashSet<Guid>>();
                var points = new Dictionary<int, Dictionary<Guid, int>>();
                foreach (var m in stageMatches)
                {
                    int gi = -1;
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(m.CustomData);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("groupIndex", out var giEl) && giEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                            gi = giEl.GetInt32();
                    }
                    catch { }
                    if (gi < 0) continue;
                    if (!groupTeams.ContainsKey(gi)) groupTeams[gi] = new HashSet<Guid>();
                    groupTeams[gi].Add(m.HomeTeamId);
                    groupTeams[gi].Add(m.AwayTeamId);
                    if (!points.ContainsKey(gi)) points[gi] = new Dictionary<Guid, int>();
                    if (!points[gi].ContainsKey(m.HomeTeamId)) points[gi][m.HomeTeamId] = 0;
                    if (!points[gi].ContainsKey(m.AwayTeamId)) points[gi][m.AwayTeamId] = 0;
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(m.CustomData);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("winnerTeamId", out var w))
                        {
                            Guid gid;
                            if (w.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var s = w.GetString();
                                if (!string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out gid)) if (points[gi].ContainsKey(gid)) points[gi][gid] += 3;
                            }
                            else if (w.ValueKind == System.Text.Json.JsonValueKind.Number)
                            {
                                var s = w.GetRawText();
                                if (Guid.TryParse(s, out gid)) if (points[gi].ContainsKey(gid)) points[gi][gid] += 3;
                            }
                        }
                    }
                    catch { }
                }

                var standingsArr = new System.Text.Json.Nodes.JsonArray();
                foreach (var kv in points.OrderBy(k => k.Key))
                {
                    var gi = kv.Key;
                    var arr = kv.Value.OrderByDescending(x => x.Value).Select(x => new System.Text.Json.Nodes.JsonObject { ["teamId"] = x.Key.ToString(), ["points"] = x.Value }).ToList();
                    var obj = new System.Text.Json.Nodes.JsonObject { ["groupIndex"] = gi, ["items"] = new System.Text.Json.Nodes.JsonArray(arr.ToArray()) };
                    standingsArr.Add(obj);
                }
                var baseNode = (!string.IsNullOrWhiteSpace(eventEntity.CustomData) ? System.Text.Json.Nodes.JsonNode.Parse(eventEntity.CustomData) as System.Text.Json.Nodes.JsonObject : null) ?? new System.Text.Json.Nodes.JsonObject();
                var gsNode = baseNode.TryGetPropertyValue("groupStage", out var existingGs) && existingGs is System.Text.Json.Nodes.JsonObject ? (System.Text.Json.Nodes.JsonObject)existingGs! : new System.Text.Json.Nodes.JsonObject();
                gsNode["standings"] = standingsArr;
                baseNode["groupStage"] = gsNode;
                eventEntity.CustomData = baseNode.ToJsonString();
                await _eventRepository.UpdateEventAsync(eventEntity);

                var advanced = new List<Guid>();
                foreach (var gi in points.Keys.OrderBy(x => x))
                {
                    var tops = points[gi].OrderByDescending(x => x.Value).Take(Math.Max(1, advancePerGroup)).Select(x => x.Key).ToList();
                    advanced.AddRange(tops);
                }
                if (advanced.Count < 2) throw new InvalidOperationException("没有足够队伍晋级");

                var pairsKnockout = new List<(Guid, Guid)>();
                for (int i = 0; i + 1 < advanced.Count; i += 2)
                    pairsKnockout.Add((advanced[i], advanced[i + 1]));

                var gsStartDate = dto.StartDate.HasValue ? dto.StartDate.Value.Date : eventEntity.CompetitionStartTime.Date;
                TimeSpan gsDailyStart = new TimeSpan(eventEntity.CompetitionStartTime.Hour, eventEntity.CompetitionStartTime.Minute, 0);
                if (!string.IsNullOrWhiteSpace(dto.DailyStartTime))
                {
                    try
                    {
                        var parts = dto.DailyStartTime.Split(':');
                        var hh = int.Parse(parts[0]);
                        var mm = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                        gsDailyStart = new TimeSpan(hh, mm, 0);
                    }
                    catch { }
                }
                var gsMaxPerDay = dto.MaxMatchesPerDay.HasValue && dto.MaxMatchesPerDay.Value > 0 ? dto.MaxMatchesPerDay.Value : int.MaxValue;
                var nowKnockout = ChinaNow();
                var createdKnockout = new List<MatchDto>();
                var gsCurrentDay = gsStartDate;
                var gsDayCount = 0;
                var gsCurrentSlot = ChinaLocalToUtc(new DateTime(gsCurrentDay.Year, gsCurrentDay.Month, gsCurrentDay.Day, gsDailyStart.Hours, gsDailyStart.Minutes, 0, DateTimeKind.Unspecified));
                DateTime BaseStartGs()
                {
                    return ChinaLocalToUtc(new DateTime(gsStartDate.Year, gsStartDate.Month, gsStartDate.Day, gsDailyStart.Hours, gsDailyStart.Minutes, 0, DateTimeKind.Unspecified));
                }
                var gsTeamNextTime = new Dictionary<Guid, DateTime>();
                foreach (var m in stageMatches)
                {
                    var t = m.MatchTime;
                    if (!gsTeamNextTime.ContainsKey(m.HomeTeamId) || gsTeamNextTime[m.HomeTeamId] < t)
                        gsTeamNextTime[m.HomeTeamId] = t.AddMinutes(interval);
                    if (!gsTeamNextTime.ContainsKey(m.AwayTeamId) || gsTeamNextTime[m.AwayTeamId] < t)
                        gsTeamNextTime[m.AwayTeamId] = t.AddMinutes(interval);
                }
                DateTime NextAvailableGs(Guid a, Guid b)
                {
                    var baseStart = BaseStartGs();
                    var tA = gsTeamNextTime.ContainsKey(a) ? gsTeamNextTime[a] : baseStart;
                    var tB = gsTeamNextTime.ContainsKey(b) ? gsTeamNextTime[b] : baseStart;
                    var t = new DateTime(Math.Max(tA.Ticks, tB.Ticks), DateTimeKind.Utc);
                    return t;
                }
                // 计算下一轮淘汰赛 bracketRound 和顺序
                int nextRoundSe = 1;
                try
                {
                    var existingSe = existing.Where(m =>
                    {
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(m.CustomData))
                            {
                                using var doc = System.Text.Json.JsonDocument.Parse(m.CustomData);
                                var root = doc.RootElement;
                                if (root.TryGetProperty("stage", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    var sv = s.GetString()?.Trim().ToLowerInvariant();
                                    return sv == "single_elim";
                                }
                            }
                        }
                        catch { }
                        return false;
                    }).ToList();
                    var maxBr = 0;
                    foreach (var em in existingSe)
                    {
                        try
                        {
                            using var doc2 = System.Text.Json.JsonDocument.Parse(em.CustomData);
                            var root2 = doc2.RootElement;
                            if (root2.TryGetProperty("bracketRound", out var brEl) && brEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                            {
                                var v = brEl.GetInt32();
                                if (v > maxBr) maxBr = v;
                            }
                        }
                        catch { }
                    }
                    nextRoundSe = Math.Max(1, maxBr + 1);
                }
                catch { nextRoundSe = 1; }

                int orderSe = 0;
                foreach (var (a, b) in pairsKnockout)
                {
                    orderSe++;
                    var t = NextAvailableGs(a, b);
                    var candidate = gsCurrentSlot;
                    var scheduled = new DateTime(Math.Max(candidate.Ticks, t.Ticks), DateTimeKind.Utc);
                    var m = new Match
                    {
                        HomeTeamId = a,
                        AwayTeamId = b,
                        MatchTime = scheduled,
                        EventId = eventId,
                        CustomData = new System.Text.Json.Nodes.JsonObject { ["stage"] = "single_elim", ["bestOf"] = bestOf, ["fromStage"] = "groups", ["bracketRound"] = nextRoundSe, ["bracketOrder"] = orderSe }.ToJsonString(),
                        CreatedAt = nowKnockout,
                        UpdatedAt = nowKnockout
                    };
                    var createdMatch = await _matchRepository.CreateMatchAsync(m);
                    createdKnockout.Add(MapToMatchDto(createdMatch));
                    gsTeamNextTime[a] = scheduled.AddMinutes(interval);
                    gsTeamNextTime[b] = scheduled.AddMinutes(interval);
                    gsDayCount++;
                    if (gsDayCount >= gsMaxPerDay)
                    {
                        gsCurrentDay = gsCurrentDay.AddDays(1);
                        gsDayCount = 0;
                        gsCurrentSlot = ChinaLocalToUtc(new DateTime(gsCurrentDay.Year, gsCurrentDay.Month, gsCurrentDay.Day, gsDailyStart.Hours, gsDailyStart.Minutes, 0, DateTimeKind.Unspecified));
                    }
                    else
                    {
                        gsCurrentSlot = scheduled.AddMinutes(interval);
                    }
                }
                return createdKnockout;
            }
            else if (stage == "swiss")
            {
                if (stageMatches.Count == 0) throw new InvalidOperationException("当前赛制没有历史赛程，无法生成下一轮瑞士轮");

                var allTeams = stageMatches.SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId }).Distinct().ToList();

                var wins = new Dictionary<Guid, int>();
                foreach (var id in allTeams) wins[id] = 0;
                foreach (var m in stageMatches)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(m.CustomData))
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(m.CustomData);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("winnerTeamId", out var w))
                            {
                                Guid gid;
                                if (w.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    var s = w.GetString();
                                    if (!string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out gid)) if (wins.ContainsKey(gid)) wins[gid]++;
                                }
                                else if (w.ValueKind == System.Text.Json.JsonValueKind.Number)
                                {
                                    var s = w.GetRawText();
                                    if (Guid.TryParse(s, out gid)) if (wins.ContainsKey(gid)) wins[gid]++;
                                }
                            }
                        }
                    }
                    catch { }
                }

                var prevPairs = new HashSet<string>();
                foreach (var m in stageMatches)
                {
                    var a = m.HomeTeamId;
                    var b = m.AwayTeamId;
                    var key = a.CompareTo(b) < 0 ? ($"{a}-{b}") : ($"{b}-{a}");
                    prevPairs.Add(key);
                }

                var groups = new Dictionary<int, List<Guid>>();
                foreach (var id in allTeams)
                {
                    var w = wins.ContainsKey(id) ? wins[id] : 0;
                    if (!groups.ContainsKey(w)) groups[w] = new List<Guid>();
                    groups[w].Add(id);
                }
                var levels = groups.Keys.OrderByDescending(x => x).ToList();
                foreach (var lvl in levels)
                    groups[lvl] = groups[lvl].OrderBy(x => x).ToList();

                for (int i = 0; i < levels.Count - 1; i++)
                {
                    var cur = levels[i];
                    var nxt = levels[i + 1];
                    if (!groups.ContainsKey(nxt)) groups[nxt] = new List<Guid>();
                    var arr = groups[cur];
                    if (arr.Count % 2 == 1)
                    {
                        var moved = arr[arr.Count - 1];
                        arr.RemoveAt(arr.Count - 1);
                        groups[nxt].Add(moved);
                    }
                }

                var pairsSwiss = new List<(Guid, Guid)>();
                foreach (var lvl in levels)
                {
                    var arr = groups[lvl];
                    var used = new HashSet<Guid>();
                    for (int i = 0; i < arr.Count; i++)
                    {
                        var a = arr[i];
                        if (used.Contains(a)) continue;
                        Guid? picked = null;
                        for (int j = i + 1; j < arr.Count; j++)
                        {
                            var b = arr[j];
                            if (used.Contains(b)) continue;
                            var k = a.CompareTo(b) < 0 ? ($"{a}-{b}") : ($"{b}-{a}");
                            if (!prevPairs.Contains(k)) { picked = b; break; }
                        }
                        if (!picked.HasValue)
                        {
                            for (int j = i + 1; j < arr.Count; j++)
                            {
                                var b = arr[j];
                                if (used.Contains(b)) { continue; }
                                picked = b; break;
                            }
                        }
                        if (picked.HasValue)
                        {
                            pairsSwiss.Add((a, picked.Value));
                            used.Add(a);
                            used.Add(picked.Value);
                        }
                    }
                }

                if (pairsSwiss.Count == 0) throw new InvalidOperationException("没有可用的配对以生成下一轮瑞士轮");

                var swStartDate = dto.StartDate.HasValue ? dto.StartDate.Value.Date : eventEntity.CompetitionStartTime.Date;
                TimeSpan swDailyStart = new TimeSpan(eventEntity.CompetitionStartTime.Hour, eventEntity.CompetitionStartTime.Minute, 0);
                if (!string.IsNullOrWhiteSpace(dto.DailyStartTime))
                {
                    try
                    {
                        var parts = dto.DailyStartTime.Split(':');
                        var hh = int.Parse(parts[0]);
                        var mm = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                        swDailyStart = new TimeSpan(hh, mm, 0);
                    }
                    catch { }
                }
                var swMaxPerDay = dto.MaxMatchesPerDay.HasValue && dto.MaxMatchesPerDay.Value > 0 ? dto.MaxMatchesPerDay.Value : int.MaxValue;

                var swTeamNextTime = new Dictionary<Guid, DateTime>();
                foreach (var m in stageMatches)
                {
                    var t = m.MatchTime;
                    if (!swTeamNextTime.ContainsKey(m.HomeTeamId) || swTeamNextTime[m.HomeTeamId] < t)
                        swTeamNextTime[m.HomeTeamId] = t.AddMinutes(interval);
                    if (!swTeamNextTime.ContainsKey(m.AwayTeamId) || swTeamNextTime[m.AwayTeamId] < t)
                        swTeamNextTime[m.AwayTeamId] = t.AddMinutes(interval);
                }
                DateTime NextAvailableSw(Guid a, Guid b)
                {
                    var tA = swTeamNextTime.ContainsKey(a) ? swTeamNextTime[a] : ChinaLocalToUtc(new DateTime(swStartDate.Year, swStartDate.Month, swStartDate.Day, swDailyStart.Hours, swDailyStart.Minutes, 0, DateTimeKind.Unspecified));
                    var tB = swTeamNextTime.ContainsKey(b) ? swTeamNextTime[b] : ChinaLocalToUtc(new DateTime(swStartDate.Year, swStartDate.Month, swStartDate.Day, swDailyStart.Hours, swDailyStart.Minutes, 0, DateTimeKind.Unspecified));
                    var t = new DateTime(Math.Max(tA.Ticks, tB.Ticks), DateTimeKind.Utc);
                    return t;
                }

                var nowSwiss = ChinaNow();
                var createdSwiss = new List<MatchDto>();
                var swCurrentDay = swStartDate;
                var swDayCount = 0;
                var swCurrentSlot = ChinaLocalToUtc(new DateTime(swCurrentDay.Year, swCurrentDay.Month, swCurrentDay.Day, swDailyStart.Hours, swDailyStart.Minutes, 0, DateTimeKind.Unspecified));

                int nextRoundSwiss = 1;
                try
                {
                    var maxBr = 0;
                    foreach (var sm in stageMatches)
                    {
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(sm.CustomData))
                            {
                                using var d = System.Text.Json.JsonDocument.Parse(sm.CustomData);
                                var root = d.RootElement;
                                if (root.TryGetProperty("bracketRound", out var brEl) && brEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                                {
                                    var v = brEl.GetInt32();
                                    if (v > maxBr) maxBr = v;
                                }
                            }
                        }
                        catch { }
                    }
                    nextRoundSwiss = Math.Max(1, maxBr + 1);
                }
                catch { nextRoundSwiss = 1; }

                int orderSwiss = 0;
                foreach (var (a, b) in pairsSwiss)
                {
                    orderSwiss++;
                    var t = NextAvailableSw(a, b);
                    var candidate = swCurrentSlot;
                    var scheduled = new DateTime(Math.Max(candidate.Ticks, t.Ticks), DateTimeKind.Utc);

                    var m = new Match
                    {
                        HomeTeamId = a,
                        AwayTeamId = b,
                        MatchTime = scheduled,
                        EventId = eventId,
                        CustomData = new System.Text.Json.Nodes.JsonObject
                        {
                            ["stage"] = "swiss",
                            ["bestOf"] = bestOf,
                            ["bracketRound"] = nextRoundSwiss,
                            ["bracketOrder"] = orderSwiss
                        }.ToJsonString(),
                        CreatedAt = nowSwiss,
                        UpdatedAt = nowSwiss
                    };

                    var createdMatch = await _matchRepository.CreateMatchAsync(m);
                    createdSwiss.Add(MapToMatchDto(createdMatch));

                    swTeamNextTime[a] = scheduled.AddMinutes(interval);
                    swTeamNextTime[b] = scheduled.AddMinutes(interval);

                    swDayCount++;
                    if (swDayCount >= swMaxPerDay)
                    {
                        swCurrentDay = swCurrentDay.AddDays(1);
                        swDayCount = 0;
                        swCurrentSlot = ChinaLocalToUtc(new DateTime(swCurrentDay.Year, swCurrentDay.Month, swCurrentDay.Day, swDailyStart.Hours, swDailyStart.Minutes, 0, DateTimeKind.Unspecified));
                    }
                    else
                    {
                        swCurrentSlot = scheduled.AddMinutes(interval);
                    }
                }

                return createdSwiss;
            }
            
            var winners = new List<(Guid teamId, DateTime winTime)>();
            foreach (var m in stageMatches)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(m.CustomData))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(m.CustomData);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("winnerTeamId", out var w))
                        {
                            Guid gid;
                            if (w.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var s = w.GetString();
                                if (!string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out gid)) winners.Add((gid, m.MatchTime));
                            }
                            else if (w.ValueKind == System.Text.Json.JsonValueKind.Number)
                            {
                                var s = w.GetRawText();
                                if (Guid.TryParse(s, out gid)) winners.Add((gid, m.MatchTime));
                            }
                        }
                    }
                }
                catch { }
            }

            var progressed = new HashSet<Guid>();
            foreach (var w in winners)
            {
                if (stageMatches.Any(x => (x.HomeTeamId == w.teamId || x.AwayTeamId == w.teamId) && x.MatchTime > w.winTime))
                {
                    progressed.Add(w.teamId);
                }
            }

            var pending = winners.Where(w => !progressed.Contains(w.teamId)).OrderBy(w => w.winTime).Select(w => w.teamId).Distinct().ToList();
            if (pending.Count < 2) throw new InvalidOperationException("没有足够的胜者以生成下一轮");

            var pairs = new List<(Guid, Guid)>();
            for (int i = 0; i + 1 < pending.Count; i += 2)
                pairs.Add((pending[i], pending[i + 1]));

            var startDate = dto.StartDate.HasValue ? dto.StartDate.Value.Date : eventEntity.CompetitionStartTime.Date;
            TimeSpan dailyStart = new TimeSpan(eventEntity.CompetitionStartTime.Hour, eventEntity.CompetitionStartTime.Minute, 0);
            if (!string.IsNullOrWhiteSpace(dto.DailyStartTime))
            {
                try
                {
                    var parts = dto.DailyStartTime.Split(':');
                    var hh = int.Parse(parts[0]);
                    var mm = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                    dailyStart = new TimeSpan(hh, mm, 0);
                }
                catch { }
            }
            var maxPerDay = dto.MaxMatchesPerDay.HasValue && dto.MaxMatchesPerDay.Value > 0 ? dto.MaxMatchesPerDay.Value : int.MaxValue;

            var teamNextTime = new Dictionary<Guid, DateTime>();
            foreach (var m in stageMatches)
            {
                var t = m.MatchTime;
                if (!teamNextTime.ContainsKey(m.HomeTeamId) || teamNextTime[m.HomeTeamId] < t)
                    teamNextTime[m.HomeTeamId] = t.AddMinutes(interval);
                if (!teamNextTime.ContainsKey(m.AwayTeamId) || teamNextTime[m.AwayTeamId] < t)
                    teamNextTime[m.AwayTeamId] = t.AddMinutes(interval);
            }

            DateTime NextAvailable(Guid a, Guid b)
            {
                var tA = teamNextTime.ContainsKey(a) ? teamNextTime[a] : ChinaLocalToUtc(new DateTime(startDate.Year, startDate.Month, startDate.Day, dailyStart.Hours, dailyStart.Minutes, 0, DateTimeKind.Unspecified));
                var tB = teamNextTime.ContainsKey(b) ? teamNextTime[b] : ChinaLocalToUtc(new DateTime(startDate.Year, startDate.Month, startDate.Day, dailyStart.Hours, dailyStart.Minutes, 0, DateTimeKind.Unspecified));
                var t = new DateTime(Math.Max(tA.Ticks, tB.Ticks), DateTimeKind.Utc);
                return t;
            }

            var now = ChinaNow();
            var created = new List<MatchDto>();
            var currentDay = startDate;
            var dayCount = 0;
            var currentSlot = ChinaLocalToUtc(new DateTime(currentDay.Year, currentDay.Month, currentDay.Day, dailyStart.Hours, dailyStart.Minutes, 0, DateTimeKind.Unspecified));

            // 计算本赛制下一轮的 bracketRound
            int nextRound = 1;
            try
            {
                var maxBr = 0;
                foreach (var sm in stageMatches)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(sm.CustomData))
                        {
                            using var d = System.Text.Json.JsonDocument.Parse(sm.CustomData);
                            var root = d.RootElement;
                            if (root.TryGetProperty("bracketRound", out var brEl) && brEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                            {
                                var v = brEl.GetInt32();
                                if (v > maxBr) maxBr = v;
                            }
                        }
                    }
                    catch { }
                }
                nextRound = Math.Max(1, maxBr + 1);
            }
            catch { nextRound = 1; }

            int order = 0;
            foreach (var (a, b) in pairs)
            {
                order++;
                var t = NextAvailable(a, b);
                var candidate = currentSlot;
                var scheduled = new DateTime(Math.Max(candidate.Ticks, t.Ticks), DateTimeKind.Utc);

                var m = new Match
                {
                    HomeTeamId = a,
                    AwayTeamId = b,
                    MatchTime = scheduled,
                    EventId = eventId,
                    CustomData = new JsonObject
                    {
                        ["stage"] = stage,
                        ["bestOf"] = bestOf,
                        ["bracketRound"] = nextRound,
                        ["bracketOrder"] = order
                    }.ToJsonString(),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                var createdMatch = await _matchRepository.CreateMatchAsync(m);
                created.Add(MapToMatchDto(createdMatch));

                teamNextTime[a] = scheduled.AddMinutes(interval);
                teamNextTime[b] = scheduled.AddMinutes(interval);

                dayCount++;
                if (dayCount >= maxPerDay)
                {
                    currentDay = currentDay.AddDays(1);
                    dayCount = 0;
                    currentSlot = ChinaLocalToUtc(new DateTime(currentDay.Year, currentDay.Month, currentDay.Day, dailyStart.Hours, dailyStart.Minutes, 0, DateTimeKind.Unspecified));
                }
                else
                {
                    currentSlot = scheduled.AddMinutes(interval);
                }
            }

            return created;
        }

        public async Task<IEnumerable<ConflictDto>> GetScheduleConflictsAsync(Guid eventId)
        {
            var count = await _matchRepository.GetMatchCountAsync(eventId);
            var matches = await _matchRepository.GetAllMatchesAsync(eventId, 1, Math.Max(1, count));
            var list = matches.OrderBy(m => m.MatchTime).ToList();
            var conflicts = new List<ConflictDto>();
            var byTeam = new Dictionary<Guid, List<Match>>();
            foreach (var m in list)
            {
                if (!byTeam.ContainsKey(m.HomeTeamId)) byTeam[m.HomeTeamId] = new List<Match>();
                if (!byTeam.ContainsKey(m.AwayTeamId)) byTeam[m.AwayTeamId] = new List<Match>();
                byTeam[m.HomeTeamId].Add(m);
                byTeam[m.AwayTeamId].Add(m);
            }

            foreach (var kv in byTeam)
            {
                var arr = kv.Value.OrderBy(x => x.MatchTime).ToList();
                for (int i = 0; i + 1 < arr.Count; i++)
                {
                    var a = arr[i];
                    var b = arr[i + 1];
                    var endA = a.MatchTime.AddHours(1);
                    if (endA > b.MatchTime)
                    {
                        conflicts.Add(new ConflictDto
                        {
                            TeamId = kv.Key,
                            MatchIds = new List<Guid> { a.Id, b.Id },
                            Start = a.MatchTime,
                            End = endA
                        });
                    }
                }
            }

            return conflicts;
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

        private static DateTime ChinaLocalToUtc(DateTime local)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
                return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);
            }
            catch
            {
                return DateTime.SpecifyKind(local, DateTimeKind.Utc).AddHours(-8);
            }
        }

        private async Task RecalculateGroupStandingsAsync(Guid eventId)
        {
            var eventEntity = await _eventRepository.GetEventByIdAsync(eventId);
            if (eventEntity == null) return;
            var count = await _matchRepository.GetMatchCountAsync(eventId);
            var existing = await _matchRepository.GetAllMatchesAsync(eventId, 1, Math.Max(1, count));
            var stageMatches = existing.Where(m =>
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(m.CustomData))
                    {
                        using var doc = JsonDocument.Parse(m.CustomData);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("stage", out var s) && s.ValueKind == JsonValueKind.String)
                        {
                            var sv = (s.GetString() ?? string.Empty).Trim().ToLowerInvariant();
                            return sv == "groups";
                        }
                    }
                }
                catch { }
                return false;
            }).ToList();

            var points = new Dictionary<int, Dictionary<Guid, int>>();
            foreach (var m in stageMatches)
            {
                int gi = -1;
                try
                {
                    using var doc = JsonDocument.Parse(m.CustomData);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("groupIndex", out var giEl) && giEl.ValueKind == JsonValueKind.Number)
                        gi = giEl.GetInt32();
                }
                catch { }
                if (gi < 0) continue;
                if (!points.ContainsKey(gi)) points[gi] = new Dictionary<Guid, int>();
                if (!points[gi].ContainsKey(m.HomeTeamId)) points[gi][m.HomeTeamId] = 0;
                if (!points[gi].ContainsKey(m.AwayTeamId)) points[gi][m.AwayTeamId] = 0;
                try
                {
                    using var doc = JsonDocument.Parse(m.CustomData);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("winnerTeamId", out var w))
                    {
                        Guid gid;
                        if (w.ValueKind == JsonValueKind.String)
                        {
                            var s = w.GetString();
                            if (!string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out gid)) if (points[gi].ContainsKey(gid)) points[gi][gid] += 3;
                        }
                        else if (w.ValueKind == JsonValueKind.Number)
                        {
                            var s = w.GetRawText();
                            if (Guid.TryParse(s, out gid)) if (points[gi].ContainsKey(gid)) points[gi][gid] += 3;
                        }
                    }
                }
                catch { }
            }

            var standingsArr = new JsonArray();
            foreach (var kv in points.OrderBy(k => k.Key))
            {
                var gi = kv.Key;
                var arr = kv.Value.OrderByDescending(x => x.Value).Select(x => new JsonObject { ["teamId"] = x.Key.ToString(), ["points"] = x.Value }).ToList();
                var obj = new JsonObject { ["groupIndex"] = gi, ["items"] = new JsonArray(arr.ToArray()) };
                standingsArr.Add(obj);
            }
            var baseNode = (!string.IsNullOrWhiteSpace(eventEntity.CustomData) ? JsonNode.Parse(eventEntity.CustomData) as JsonObject : null) ?? new JsonObject();
            var gsNode = baseNode.TryGetPropertyValue("groupStage", out var existingGs) && existingGs is JsonObject ? (JsonObject)existingGs! : new JsonObject();
            gsNode["standings"] = standingsArr;
            baseNode["groupStage"] = gsNode;
            eventEntity.CustomData = baseNode.ToJsonString();
            await _eventRepository.UpdateEventAsync(eventEntity);
        }
    }
}
