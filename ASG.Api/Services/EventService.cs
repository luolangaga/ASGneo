using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Repositories;
using Microsoft.AspNetCore.Identity;
using System.Net;
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
        private readonly IEmailService _emailService;
        private readonly INotificationService _notify;
        private readonly Microsoft.Extensions.Logging.ILogger<EventService> _logger;

        public EventService(IEventRepository eventRepository, ITeamRepository teamRepository, UserManager<User> userManager, IEmailService emailService, Microsoft.Extensions.Logging.ILogger<EventService> logger, INotificationService notify)
        {
            _eventRepository = eventRepository;
            _teamRepository = teamRepository;
            _userManager = userManager;
            _emailService = emailService;
            _logger = logger;
            _notify = notify;
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

            var regStart = createEventDto.RegistrationStartTime;
            var regEnd = createEventDto.RegistrationEndTime;
            var compStart = createEventDto.CompetitionStartTime;
            var compEnd = createEventDto.CompetitionEndTime.HasValue
                ? createEventDto.CompetitionEndTime.Value
                : (DateTime?)null;

            ValidateEventTimes(regStart, regEnd, compStart, compEnd);

            var eventEntity = new Event
            {
                Id = Guid.NewGuid(),
                Name = createEventDto.Name,
                Description = createEventDto.Description,
                QqGroup = createEventDto.QqGroup,
                RulesMarkdown = createEventDto.RulesMarkdown,
                RegistrationStartTime = regStart,
                RegistrationEndTime = regEnd,
                CompetitionStartTime = compStart,
                CompetitionEndTime = compEnd,
                MaxTeams = createEventDto.MaxTeams,
                Status = EventStatus.Draft,
                CreatedAt = ChinaNow(),
                UpdatedAt = ChinaNow(),
                CreatedByUserId = creatorUserId,
                RegistrationMode = createEventDto.RegistrationMode
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

            var regStart = updateEventDto.RegistrationStartTime;
            var regEnd = updateEventDto.RegistrationEndTime;
            var compStart = updateEventDto.CompetitionStartTime;
            var compEnd = updateEventDto.CompetitionEndTime.HasValue
                ? updateEventDto.CompetitionEndTime.Value
                : (DateTime?)null;

            ValidateEventTimes(regStart, regEnd, compStart, compEnd);

            // 更新赛事信息
            eventEntity.Name = updateEventDto.Name;
            eventEntity.Description = updateEventDto.Description;
            eventEntity.QqGroup = updateEventDto.QqGroup;
            eventEntity.RulesMarkdown = updateEventDto.RulesMarkdown;
            eventEntity.RegistrationStartTime = regStart;
            eventEntity.RegistrationEndTime = regEnd;
            eventEntity.CompetitionStartTime = compStart;
            eventEntity.CompetitionEndTime = compEnd;
            eventEntity.MaxTeams = updateEventDto.MaxTeams;
            eventEntity.Status = updateEventDto.Status;
            eventEntity.RegistrationMode = updateEventDto.RegistrationMode;

            var updatedEvent = await _eventRepository.UpdateEventAsync(eventEntity);
            return MapToEventDto(updatedEvent);
        }

        public async Task<bool> DeleteEventAsync(Guid id, string userId)
        {
            var eventEntity = await _eventRepository.GetEventByIdAsync(id);
            if (eventEntity == null)
                return false;

            var user = await _userManager.FindByIdAsync(userId);
            var isAdmin = user != null && (user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin);
            if (!(eventEntity.CreatedByUserId == userId || isAdmin))
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
            var now = ChinaNow();
            if (eventEntity.Status != EventStatus.RegistrationOpen)
            {
                throw new InvalidOperationException("赛事未开放报名");
            }

            var eventRegStart = eventEntity.RegistrationStartTime;
            var eventRegEnd = eventEntity.RegistrationEndTime;

            if (now < eventRegStart || now > eventRegEnd)
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

            var (minReg, maxReg, minSur, maxSur) = GetPlayerTypeRequirements(eventEntity);
            if (minReg.HasValue || minSur.HasValue)
            {
                var teamPlayers = await _teamRepository.GetTeamPlayersAsync(registerDto.TeamId);
                var regulatorCount = teamPlayers.Count(p => p.PlayerType == PlayerType.Regulator);
                var survivorCount = teamPlayers.Count(p => p.PlayerType == PlayerType.Survivor || (int)p.PlayerType == 0);
                if (minReg.HasValue && regulatorCount < minReg.Value)
                {
                    throw new InvalidOperationException($"不满足报名要求：监管者至少 {minReg.Value} 名");
                }
                if (minSur.HasValue && survivorCount < minSur.Value)
                {
                    throw new InvalidOperationException($"不满足报名要求：求生者至少 {minSur.Value} 名");
                }
                if ((maxReg.HasValue && regulatorCount > (maxReg.Value)) || (maxSur.HasValue && survivorCount > (maxSur.Value)))
                {
                    var answers = await _eventRepository.GetRegistrationAnswersAsync(eventId, registerDto.TeamId);
                    var json = answers?.AnswersJson ?? "{}";
                    List<Guid> selected = new List<Guid>();
                    try
                    {
                        using var docSel = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
                        var rootSel = docSel.RootElement;
                        if (rootSel.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            System.Text.Json.JsonElement sel;
                            if (rootSel.TryGetProperty("selectedPlayerIds", out sel)) { }
                            else if (rootSel.TryGetProperty("SelectedPlayerIds", out sel)) { }
                            if (sel.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var e in sel.EnumerateArray())
                                {
                                    if (e.ValueKind == System.Text.Json.JsonValueKind.String && Guid.TryParse(e.GetString(), out var gid)) selected.Add(gid);
                                }
                            }
                        }
                    }
                    catch { }
                    if (selected.Count == 0)
                    {
                        throw new InvalidOperationException("队员人数超过上限，请在报名信息中选择参赛队员后再提交");
                    }
                    var idSet = new HashSet<Guid>(teamPlayers.Select(p => p.Id));
                    // 仅统计选择的队员类型数量
                    var selReg = teamPlayers.Count(p => selected.Contains(p.Id) && p.PlayerType == PlayerType.Regulator);
                    var selSur = teamPlayers.Count(p => selected.Contains(p.Id) && (p.PlayerType == PlayerType.Survivor || (int)p.PlayerType == 0));
                    if (maxReg.HasValue && selReg > maxReg.Value)
                    {
                        throw new InvalidOperationException($"已选择的监管者超过上限：最多 {maxReg.Value} 名");
                    }
                    if (maxSur.HasValue && selSur > maxSur.Value)
                    {
                        throw new InvalidOperationException($"已选择的求生者超过上限：最多 {maxSur.Value} 名");
                    }
                }
            }

            var teamEvent = new TeamEvent
            {
                TeamId = registerDto.TeamId,
                EventId = eventId,
                RegistrationTime = ChinaNow(),
                Status = RegistrationStatus.Pending,
                Notes = registerDto.Notes,
                RegisteredByUserId = userId
            };

            var createdTeamEvent = await _eventRepository.RegisterTeamToEventAsync(teamEvent);
            return MapToTeamEventDto(createdTeamEvent, team.Name, eventEntity.Name);
        }

        public async Task<PlayerEventDto> RegisterPlayerToEventAsync(Guid eventId, RegisterPlayerToEventDto dto, string userId)
        {
            var ev = await _eventRepository.GetEventByIdAsync(eventId);
            if (ev == null) throw new InvalidOperationException("赛事不存在");
            if (ev.RegistrationMode != RegistrationMode.Solo) throw new InvalidOperationException("该赛事不支持单人报名");

            var now = ChinaNow();
            if (ev.Status != EventStatus.RegistrationOpen) throw new InvalidOperationException("赛事未开放报名");
            if (now < ev.RegistrationStartTime || now > ev.RegistrationEndTime) throw new InvalidOperationException("不在报名时间范围内");

            Player? player = null;
            if (dto.PlayerId.HasValue)
            {
                player = await _teamRepository.GetPlayerByIdAsync(dto.PlayerId.Value);
            }
            else
            {
                player = await _teamRepository.GetPlayerByUserIdAsync(userId);
            }
            if (player == null) throw new InvalidOperationException("未找到您的玩家信息");
            if (!string.Equals(player.UserId, userId, StringComparison.Ordinal)) throw new UnauthorizedAccessException("只能使用自己的玩家报名");

            if (await _eventRepository.IsPlayerRegisteredAsync(player.Id, eventId)) throw new InvalidOperationException("您已报名该赛事");

            var pe = new PlayerEvent
            {
                PlayerId = player.Id,
                EventId = eventId,
                RegistrationTime = now,
                Status = RegistrationStatus.Pending,
                Notes = dto.Notes,
                RegisteredByUserId = userId
            };
            var saved = await _eventRepository.RegisterPlayerToEventAsync(pe);
            return new PlayerEventDto
            {
                PlayerId = saved.PlayerId,
                EventId = saved.EventId,
                PlayerName = player.Name,
                RegistrationTime = saved.RegistrationTime,
                Status = saved.Status,
                Notes = saved.Notes,
                RegisteredByUserId = saved.RegisteredByUserId
            };
        }

        public async Task<IEnumerable<PlayerEventDto>> GetEventPlayerRegistrationsAsync(Guid eventId)
        {
            var list = await _eventRepository.GetEventPlayerRegistrationsAsync(eventId);
            return list.Select(pe => new PlayerEventDto
            {
                PlayerId = pe.PlayerId,
                EventId = pe.EventId,
                PlayerName = pe.Player?.Name ?? string.Empty,
                RegistrationTime = pe.RegistrationTime,
                Status = pe.Status,
                Notes = pe.Notes,
                RegisteredByUserId = pe.RegisteredByUserId
            });
        }

        public async Task<TeamEventDto> CreateSoloTemporaryTeamAsync(Guid eventId, CreateSoloTempTeamDto dto, string userId)
        {
            var ev = await _eventRepository.GetEventByIdAsync(eventId);
            if (ev == null) throw new InvalidOperationException("赛事不存在");
            if (ev.RegistrationMode != RegistrationMode.Solo) throw new InvalidOperationException("该赛事不支持临时战队");
            if (!await CanUserManageEventAsync(eventId, userId)) throw new UnauthorizedAccessException("无权限");

            var regs = await _eventRepository.GetEventPlayerRegistrationsAsync(eventId);
            var regSet = regs.Select(r => r.PlayerId).ToHashSet();
            foreach (var pid in dto.PlayerIds)
            {
                if (!regSet.Contains(pid)) throw new InvalidOperationException("所选玩家尚未报名该赛事");
            }

            var baseName = string.IsNullOrWhiteSpace(dto.Name) ? "临时战队" : dto.Name!.Trim();
            var name = baseName;
            var suffix = 0;
            while (await _teamRepository.TeamNameExistsAsync(name))
            {
                suffix++;
                name = $"{baseName}-{suffix}";
                if (suffix > 10) break;
            }

            var tempTeam = new Team
            {
                Id = Guid.NewGuid(),
                Name = name,
                Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                Description = "赛事临时战队",
                OwnerId = null,
                UserId = null,
                IsTemporary = true,
                TemporaryEventId = eventId,
                Players = new List<Player>()
            };

            foreach (var pid in dto.PlayerIds)
            {
                var p = await _teamRepository.GetPlayerByIdAsync(pid);
                if (p == null) throw new InvalidOperationException("玩家不存在");
                var clone = new Player
                {
                    Id = Guid.NewGuid(),
                    Name = p.Name,
                    GameId = p.GameId,
                    GameRank = p.GameRank,
                    Description = p.Description,
                    PlayerType = p.PlayerType,
                    TeamId = tempTeam.Id,
                    UserId = p.UserId,
                    CreatedAt = ChinaNow(),
                    UpdatedAt = ChinaNow()
                };
                tempTeam.Players.Add(clone);
            }

            var createdTeam = await _teamRepository.CreateTeamAsync(tempTeam);
            var te = new TeamEvent
            {
                TeamId = createdTeam.Id,
                EventId = eventId,
                RegistrationTime = ChinaNow(),
                Status = dto.ApproveRegistration ? RegistrationStatus.Approved : RegistrationStatus.Pending,
                Notes = "临时战队",
                RegisteredByUserId = userId
            };
            var savedTe = await _eventRepository.RegisterTeamToEventAsync(te);
            return MapToTeamEventDto(savedTe, createdTeam.Name, ev.Name);
        }

        private static (int? minReg, int? maxReg, int? minSur, int? maxSur) GetPlayerTypeRequirements(Event ev)
        {
            if (string.IsNullOrWhiteSpace(ev.CustomData)) return (null, null, null, null);
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(ev.CustomData);
                var root = doc.RootElement;
                if (root.ValueKind != System.Text.Json.JsonValueKind.Object) return (null, null, null, null);
                System.Text.Json.JsonElement cfg;
                if (root.TryGetProperty("playerTypeRequirements", out cfg))
                {
                    if (cfg.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var s = cfg.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            try
                            {
                                using var inner = System.Text.Json.JsonDocument.Parse(s);
                                cfg = inner.RootElement.Clone();
                            }
                            catch { }
                        }
                    }
                    int? ReadInt(System.Text.Json.JsonElement obj, string key)
                    {
                        if (obj.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
                        if (obj.TryGetProperty(key, out var v))
                        {
                            if (v.ValueKind == System.Text.Json.JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
                            if (v.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(v.GetString(), out var si)) return si;
                        }
                        foreach (var p in obj.EnumerateObject())
                        {
                            if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
                            {
                                var el = p.Value;
                                if (el.ValueKind == System.Text.Json.JsonValueKind.Number && el.TryGetInt32(out var ii)) return ii;
                                if (el.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(el.GetString(), out var sii)) return sii;
                            }
                        }
                        return null;
                    }

                    int? minReg = null, maxReg = null, minSur = null, maxSur = null;
                    if (cfg.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        if (cfg.TryGetProperty("regulator", out var reg))
                        {
                            minReg = ReadInt(reg, "min");
                            maxReg = ReadInt(reg, "max");
                        }
                        foreach (var p in cfg.EnumerateObject())
                        {
                            if (string.Equals(p.Name, "regulator", StringComparison.OrdinalIgnoreCase))
                            {
                                minReg ??= ReadInt(p.Value, "min");
                                maxReg ??= ReadInt(p.Value, "max");
                            }
                            if (string.Equals(p.Name, "survivor", StringComparison.OrdinalIgnoreCase))
                            {
                                minSur ??= ReadInt(p.Value, "min");
                                maxSur ??= ReadInt(p.Value, "max");
                            }
                        }
                        if (!cfg.TryGetProperty("survivor", out var sur))
                        {
                            // already tried case-insensitive above
                        }
                        else
                        {
                            minSur ??= ReadInt(sur, "min");
                            maxSur ??= ReadInt(sur, "max");
                        }
                    }
                    return (minReg, maxReg, minSur, maxSur);
                }
            }
            catch { }
            return (null, null, null, null);
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

            // 邮件通知与积分扣减（Approved 状态默认通知）
            var shouldNotify = updateDto.NotifyByEmail || updateDto.Status == RegistrationStatus.Approved;
            if (shouldNotify && team?.OwnerId != null)
            {
                // 收件人：战队拥有者；扣费方：赛事创建者
                var teamOwner = await _userManager.FindByIdAsync(team.OwnerId);
                var toEmail = teamOwner?.Email;
                var eventOwnerId = eventEntity?.CreatedByUserId;
                var eventOwner = eventOwnerId != null ? await _userManager.FindByIdAsync(eventOwnerId) : null;

                // 没有赛事创建者或其账户不存在，跳过发送并记录
                if (string.IsNullOrWhiteSpace(toEmail) || teamOwner == null || eventOwner == null)
                {
                    _logger?.LogWarning("Skip email: missing recipient or event owner. EventId={EventId}, TeamId={TeamId}, EventOwnerId={EventOwnerId}", eventId, teamId, eventOwnerId);
                }
                else
                {
                    _logger?.LogInformation("Notify email requested: EventId={EventId}, TeamId={TeamId}, Status={Status}, Notify={Notify}", eventId, teamId, updateDto.Status, shouldNotify);
                    // 由赛事创建者的积分决定是否发送
                    if (eventOwner.EmailCredits > 0)
                    {
                        var statusText = updateDto.Status switch
                        {
                            RegistrationStatus.Approved => "审核通过",
                            RegistrationStatus.Rejected => "审核拒绝",
                            RegistrationStatus.Pending => "待审核",
                            _ => updateDto.Status.ToString()
                        };

                        var subject = $"赛事报名状态更新：{team?.Name} - {statusText}";
                        var sb = new StringBuilder();
                        sb.AppendLine($"尊敬的{teamOwner.FullName ?? teamOwner.UserName}, 您的战队 \"{team?.Name}\" 在赛事 \"{eventEntity?.Name}\" 的报名状态已更新为：{statusText}。");
                        if (!string.IsNullOrWhiteSpace(updateDto.Notes))
                        {
                            sb.AppendLine($"备注：{updateDto.Notes}");
                        }
                        sb.AppendLine("如有疑问请联系赛事管理员。");

                        var safeNotes = string.IsNullOrWhiteSpace(updateDto.Notes)
                            ? string.Empty
                            : WebUtility.HtmlEncode(updateDto.Notes);

                        var htmlBody = $@"<!doctype html>
<html lang='zh-CN'>
<head>
  <meta charset='utf-8'>
  <meta name='viewport' content='width=device-width,initial-scale=1'>
  <title>{subject}</title>
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, 'Noto Sans', 'Microsoft YaHei', sans-serif; background:#f5f7fb; margin:0; padding:24px; }}
    .container {{ max-width:680px; margin:0 auto; background:#ffffff; border-radius:12px; box-shadow:0 6px 18px rgba(0,0,0,.08); overflow:hidden; }}
    .header {{ background:#0d6efd; color:#fff; padding:16px 24px; font-size:18px; font-weight:600; }}
    .content {{ padding:24px; color:#1f2937; line-height:1.7; }}
    .item {{ margin:10px 0; }}
    .badge {{ display:inline-block; padding:4px 10px; border-radius:999px; background:#e9f2ff; color:#0d6efd; font-weight:600; font-size:12px; }}
    .footer {{ padding:16px 24px; font-size:12px; color:#6b7280; background:#f9fafb; }}
    strong {{ color:#111827; }}
  </style>
<!-- 邮件模板 -->
</head>
<body>
  <div class='container'>
    <div class='header'>赛事报名状态更新</div>
    <div class='content'>
      <p>尊敬的 {teamOwner.FullName ?? teamOwner.UserName}，</p>
      <p class='item'>您的战队 <strong>{team?.Name}</strong> 在赛事 <strong>{eventEntity?.Name}</strong> 的报名状态更新为：<span class='badge'>{statusText}</span></p>
      {(string.IsNullOrEmpty(safeNotes) ? string.Empty : $"<p class='item'>备注：{safeNotes}</p>")}
      <p class='item'>如有疑问请联系赛事管理员。</p>
    </div>
    <div class='footer'>此邮件由系统自动发送，请勿直接回复。</div>
  </div>
</body>
</html>";

                        try
                        {
                            var sent = await _emailService.SendHtmlAsync(toEmail!, subject, htmlBody, sb.ToString());
                            if (sent)
                            {
                                _logger?.LogInformation("Email sent successfully: EventId={EventId}, TeamId={TeamId}, To={To}, Subject={Subject}", eventId, teamId, toEmail, subject);
                                // 扣减赛事创建者积分并持久化
                                eventOwner.EmailCredits -= 1;
                                await _userManager.UpdateAsync(eventOwner);
                                _logger?.LogInformation("Credits deducted: EventOwnerId={EventOwnerId}, CreditsLeft={Credits}", eventOwner.Id, eventOwner.EmailCredits);
                            }
                            else
                            {
                                _logger?.LogWarning("Email not sent due to configuration or precondition: EventId={EventId}, TeamId={TeamId}, To={To}", eventId, teamId, toEmail);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Email send failed: EventId={EventId}, TeamId={TeamId}, To={To}, Subject={Subject}", eventId, teamId, toEmail, subject);
                            // 发送失败不影响业务流程
                        }
                    }
                    else
                    {
                        _logger?.LogWarning("Skip email due to insufficient credits: EventOwnerId={EventOwnerId}, Credits={Credits}", eventOwner.Id, eventOwner.EmailCredits);
                        // 邮件积分不足：跳过通知，不抛出异常以免影响更新流程
                    }
                }
            }

            if (updateDto.Status == RegistrationStatus.Approved)
            {
                var toUserId = updatedTeamEvent.RegisteredByUserId ?? team?.OwnerId;
                if (!string.IsNullOrEmpty(toUserId))
                {
                    var payload = System.Text.Json.JsonSerializer.Serialize(new { eventId = eventEntity?.Id, eventName = eventEntity?.Name, teamId = team?.Id, teamName = team?.Name, status = "approved" });
                    await _notify.NotifyUserAsync(toUserId, "event.registration.approved", payload);
                }
            }
            return MapToTeamEventDto(updatedTeamEvent, team?.Name ?? "", eventEntity?.Name ?? "");
        }

        public async Task<IEnumerable<TeamEventDto>> GetEventRegistrationsAsync(Guid eventId)
        {
            var registrations = await _eventRepository.GetEventRegistrationsAsync(eventId);
            var list = new List<TeamEventDto>();
            foreach (var te in registrations)
            {
                var dto = MapToTeamEventDto(te, te.Team?.Name ?? "", te.Event?.Name ?? "");
                dto.TeamHasDispute = te.Team?.HasDispute ?? false;
                dto.TeamDisputeDetail = te.Team?.DisputeDetail;
                dto.TeamCommunityPostId = te.Team?.CommunityPostId;
                var (avg, count) = await _teamRepository.GetTeamRatingSummaryAsync(te.TeamId);
                dto.TeamRatingAverage = avg;
                dto.TeamRatingCount = count;
                list.Add(dto);
            }
            return list;
        }

        public async Task<IEnumerable<TeamEventDto>> GetEventRegistrationsWithSensitiveAsync(Guid eventId, string? userId)
        {
            var registrations = await _eventRepository.GetEventRegistrationsAsync(eventId);
            var canViewFull = false;
            if (!string.IsNullOrEmpty(userId))
            {
                canViewFull = await CanUserManageEventAsync(eventId, userId);
            }

            string Mask(string? qq)
            {
                if (string.IsNullOrWhiteSpace(qq)) return string.Empty;
                var s = qq.Trim();
                if (s.Length <= 7) return s;
                var prefix = s.Substring(0, Math.Min(3, s.Length));
                var suffixLen = Math.Min(4, s.Length - prefix.Length);
                var suffix = s.Substring(s.Length - suffixLen, suffixLen);
                return $"{prefix}****{suffix}";
            }

            var list = new List<TeamEventDto>();
            foreach (var te in registrations)
            {
                var dto = MapToTeamEventDto(te, te.Team?.Name ?? "", te.Event?.Name ?? "");
                var qq = te.Team?.QqNumber;
                dto.QqNumberMasked = Mask(qq);
                dto.QqNumberFull = canViewFull ? qq : null;
                dto.TeamHasDispute = te.Team?.HasDispute ?? false;
                dto.TeamDisputeDetail = te.Team?.DisputeDetail;
                dto.TeamCommunityPostId = te.Team?.CommunityPostId;
                var (avg, count) = await _teamRepository.GetTeamRatingSummaryAsync(te.TeamId);
                dto.TeamRatingAverage = avg;
                dto.TeamRatingCount = count;
                list.Add(dto);
            }
            return list;
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

        public async Task<PagedResult<EventDto>> SearchEventsAsync(string query, int page = 1, int pageSize = 12)
        {
            var events = await _eventRepository.SearchEventsAsync(query, page, pageSize);
            var totalCount = await _eventRepository.GetSearchEventsCountAsync(query);
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
            if (user != null && (user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin))
                return true;

            if (eventEntity.EventAdmins != null && eventEntity.EventAdmins.Any(a => a.UserId == userId))
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

            // 兼容旧数据：旧字段 UserId 表示拥有者
            if (string.IsNullOrEmpty(team.OwnerId) && team.UserId == userId)
                return true;

            // 战队成员可以管理
            var players = await _teamRepository.GetTeamPlayersAsync(teamId);
            if (players.Any(p => p.UserId == userId))
                return true;

            // 系统管理员也可管理（用于紧急处理）
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && (user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin))
                return true;

            return false;
        }

        public async Task<byte[]> ExportEventRegistrationsCsvAsync(Guid eventId, string userId)
        {
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

            var schemaFields = new List<(string key, string label)>();
            try
            {
                if (!string.IsNullOrWhiteSpace(eventEntity.CustomData))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(eventEntity.CustomData);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("registrationFormSchema", out var sf))
                    {
                        var schemaJson = sf.ValueKind == System.Text.Json.JsonValueKind.String ? (sf.GetString() ?? "{}") : sf.GetRawText();
                        using var sd = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(schemaJson) ? "{}" : schemaJson);
                        if (sd.RootElement.TryGetProperty("fields", out var fields) && fields.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var f in fields.EnumerateArray())
                            {
                                string? key = null; string? label = null;
                                if (f.TryGetProperty("id", out var pid)) key = pid.GetString();
                                else if (f.TryGetProperty("Id", out var pId)) key = pId.GetString();
                                if (f.TryGetProperty("label", out var pl)) label = pl.GetString();
                                else if (f.TryGetProperty("Label", out var pL)) label = pL.GetString();
                                if (!string.IsNullOrWhiteSpace(key)) schemaFields.Add((key!, string.IsNullOrWhiteSpace(label) ? key! : label!));
                            }
                        }
                    }
                }
            }
            catch { }

            var header = new List<string>
            {
                "EventName","EventId","TeamName","TeamId","QQNumber","RegistrationStatus","RegistrationTime","Notes",
                "PlayerId","PlayerName","GameId","GameRank","PlayerDescription"
            };
            foreach (var f in schemaFields) header.Add(f.label);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", header.Select(CsvEscape)));

            foreach (var te in registrations.OrderBy(te => te.Team?.Name ?? string.Empty))
            {
                var teamName = te.Team?.Name ?? string.Empty;
                var teamQq = te.Team?.QqNumber ?? string.Empty;
                var players = await _teamRepository.GetTeamPlayersAsync(te.TeamId);

                string[] answerCols;
                List<Guid> selectedPlayerIds = new List<Guid>();
                try
                {
                    var ans = await _eventRepository.GetRegistrationAnswersAsync(eventId, te.TeamId);
                    var json = ans?.AnswersJson ?? "{}";
                    using var docAns = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
                    var rootAns = docAns.RootElement;
                    if (rootAns.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        System.Text.Json.JsonElement sel;
                        if (rootAns.TryGetProperty("selectedPlayerIds", out sel) || rootAns.TryGetProperty("SelectedPlayerIds", out sel))
                        {
                            if (sel.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var e in sel.EnumerateArray())
                                {
                                    if (e.ValueKind == System.Text.Json.JsonValueKind.String)
                                    {
                                        if (Guid.TryParse(e.GetString(), out var gid)) selectedPlayerIds.Add(gid);
                                    }
                                }
                            }
                        }
                    }
                    string ToStr(System.Text.Json.JsonElement el)
                    {
                        switch (el.ValueKind)
                        {
                            case System.Text.Json.JsonValueKind.String:
                                return el.GetString() ?? string.Empty;
                            case System.Text.Json.JsonValueKind.Number:
                                return el.ToString();
                            case System.Text.Json.JsonValueKind.True:
                                return "true";
                            case System.Text.Json.JsonValueKind.False:
                                return "false";
                            case System.Text.Json.JsonValueKind.Array:
                                var arr = el.EnumerateArray().Select(e => ToStr(e));
                                return string.Join(";", arr);
                            case System.Text.Json.JsonValueKind.Object:
                                return el.GetRawText();
                            case System.Text.Json.JsonValueKind.Null:
                                return string.Empty;
                            default:
                                return el.GetRawText();
                        }
                    }
                    answerCols = schemaFields.Select(f =>
                    {
                        string val = string.Empty;
                        if (rootAns.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            if (rootAns.TryGetProperty(f.key, out var v)) val = ToStr(v);
                            else
                            {
                                foreach (var p in rootAns.EnumerateObject())
                                {
                                    if (string.Equals(p.Name, f.key, StringComparison.OrdinalIgnoreCase))
                                    {
                                        val = ToStr(p.Value);
                                        break;
                                    }
                                }
                            }
                        }
                        return CsvEscape(val);
                    }).ToArray();
                }
                catch
                {
                    answerCols = schemaFields.Select(f => CsvEscape(string.Empty)).ToArray();
                }

                if (selectedPlayerIds.Count > 0)
                {
                    players = players.Where(p => selectedPlayerIds.Contains(p.Id)).ToList();
                }

                if (players == null || !players.Any())
                {
                    var row = new List<string>
                    {
                        CsvEscape(eventEntity.Name), CsvEscape(eventEntity.Id.ToString()), CsvEscape(teamName), CsvEscape(te.TeamId.ToString()), CsvEscape(teamQq),
                        CsvEscape(te.Status.ToString()), CsvEscape(te.RegistrationTime.ToString("yyyy-MM-ddTHH:mm:ss")), CsvEscape(te.Notes ?? string.Empty),
                        CsvEscape(string.Empty), CsvEscape(string.Empty), CsvEscape(string.Empty), CsvEscape(string.Empty), CsvEscape(string.Empty)
                    };
                    row.AddRange(answerCols);
                    sb.AppendLine(string.Join(",", row));
                    sb.AppendLine("");
                }
                else
                {
                    foreach (var p in players)
                    {
                        var row = new List<string>
                        {
                            CsvEscape(eventEntity.Name), CsvEscape(eventEntity.Id.ToString()), CsvEscape(teamName), CsvEscape(te.TeamId.ToString()), CsvEscape(teamQq),
                            CsvEscape(te.Status.ToString()), CsvEscape(te.RegistrationTime.ToString("yyyy-MM-ddTHH:mm:ss")), CsvEscape(te.Notes ?? string.Empty),
                            CsvEscape(p.Id.ToString()), CsvEscape(p.Name ?? string.Empty), CsvEscape(p.GameId ?? string.Empty), CsvEscape(p.GameRank ?? string.Empty), CsvEscape(p.Description ?? string.Empty)
                        };
                        row.AddRange(answerCols);
                        sb.AppendLine(string.Join(",", row));
                    }
                    sb.AppendLine("");
                }
            }

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return encoding.GetBytes(sb.ToString());
        }

        private static string CsvEscape(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            var escaped = s.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        private static DateTime ChinaNow()
        {
            return DateTime.UtcNow;
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
                QqGroup = eventEntity.QqGroup,
                RulesMarkdown = eventEntity.RulesMarkdown,
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
                RegisteredTeams = eventEntity.TeamEvents?.Select(te => MapToTeamEventDto(te, te.Team?.Name ?? "", eventEntity.Name)).ToList(),
                AdminUserIds = eventEntity.EventAdmins?.Select(a => a.UserId).ToList(),
                CustomData = eventEntity.CustomData,
                RegistrationMode = eventEntity.RegistrationMode
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

        public async Task<IEnumerable<UserResponseDto>> GetEventAdminsAsync(Guid eventId)
        {
            var ids = await _eventRepository.GetEventAdminUserIdsAsync(eventId);
            var list = new List<UserResponseDto>();
            foreach (var id in ids)
            {
                var u = await _userManager.FindByIdAsync(id);
                if (u != null)
                {
                    list.Add(new UserResponseDto
                    {
                        Id = u.Id,
                        Email = u.Email ?? string.Empty,
                        FullName = u.FullName,
                        Role = u.Role,
                        RoleDisplayName = u.RoleDisplayName,
                        RoleName = u.RoleName,
                        CreatedAt = u.CreatedAt,
                        UpdatedAt = u.UpdatedAt,
                        IsActive = u.IsActive,
                        AvatarUrl = null,
                        TeamId = u.TeamId,
                        EmailCredits = u.EmailCredits
                    });
                }
            }
            return list;
        }

        public async Task<bool> AddEventAdminAsync(Guid eventId, string userId, string targetUserId)
        {
            var ev = await _eventRepository.GetEventByIdAsync(eventId);
            if (ev == null) throw new InvalidOperationException("赛事不存在");
            var actor = await _userManager.FindByIdAsync(userId);
            var isAdmin = actor != null && await _userManager.IsInRoleAsync(actor, "Admin");
            if (!(ev.CreatedByUserId == userId || isAdmin)) throw new UnauthorizedAccessException("无权限设置赛事管理员");
            var target = await _userManager.FindByIdAsync(targetUserId);
            if (target == null) throw new InvalidOperationException("目标用户不存在");
            if (ev.CreatedByUserId == targetUserId) return true;
            return await _eventRepository.AddEventAdminAsync(eventId, targetUserId);
        }

        public async Task<bool> RemoveEventAdminAsync(Guid eventId, string userId, string targetUserId)
        {
            var ev = await _eventRepository.GetEventByIdAsync(eventId);
            if (ev == null) throw new InvalidOperationException("赛事不存在");
            var actor = await _userManager.FindByIdAsync(userId);
            var isAdmin = actor != null && await _userManager.IsInRoleAsync(actor, "Admin");
            if (!(ev.CreatedByUserId == userId || isAdmin)) throw new UnauthorizedAccessException("无权限移除赛事管理员");
            if (ev.CreatedByUserId == targetUserId) throw new InvalidOperationException("不能移除赛事创建者");
            return await _eventRepository.RemoveEventAdminAsync(eventId, targetUserId);
        }

        public async Task<bool> IsEventAdminAsync(Guid eventId, string userId)
        {
            return await _eventRepository.IsEventAdminAsync(eventId, userId);
        }

        // 赛程图画布持久化
        public async Task<bool> SaveBracketCanvasAsync(Guid eventId, string userId, string canvasJson)
        {
            var ev = await _eventRepository.GetEventByIdAsync(eventId);
            if (ev == null) throw new InvalidOperationException("赛事不存在");
            if (!await CanUserManageEventAsync(eventId, userId)) throw new UnauthorizedAccessException("无权限保存赛程图");

            System.Text.Json.Nodes.JsonObject node;
            try
            {
                node = (!string.IsNullOrWhiteSpace(ev.CustomData) ? System.Text.Json.Nodes.JsonNode.Parse(ev.CustomData) as System.Text.Json.Nodes.JsonObject : null) ?? new System.Text.Json.Nodes.JsonObject();
            }
            catch
            {
                node = new System.Text.Json.Nodes.JsonObject();
            }

            try
            {
                var canvasNode = System.Text.Json.Nodes.JsonNode.Parse(string.IsNullOrWhiteSpace(canvasJson) ? "{}" : canvasJson);
                node["bracketCanvas"] = canvasNode;
            }
            catch
            {
                node["bracketCanvas"] = new System.Text.Json.Nodes.JsonObject();
            }

            ev.CustomData = node.ToJsonString();
            await _eventRepository.UpdateEventAsync(ev);
            return true;
        }

        public async Task<string> GetBracketCanvasAsync(Guid eventId)
        {
            var ev = await _eventRepository.GetEventByIdAsync(eventId);
            if (ev == null) throw new InvalidOperationException("赛事不存在");
            try
            {
                if (!string.IsNullOrWhiteSpace(ev.CustomData))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(ev.CustomData);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("bracketCanvas", out var bc))
                        return bc.GetRawText();
                }
            }
            catch { }
            return "{}";
        }

        public async Task<bool> CanUserManageAnyEventOfTeamAsync(Guid teamId, string userId)
        {
            var regs = await _eventRepository.GetTeamRegistrationsAsync(teamId);
            foreach (var te in regs)
            {
                if (await CanUserManageEventAsync(te.EventId, userId)) return true;
            }
            return false;
        }

        // 规则版本化
        public async Task<RuleRevisionDto> CreateRuleRevisionAsync(Guid eventId, CreateRuleRevisionDto dto, string userId)
        {
            if (!await CanUserManageEventAsync(eventId, userId)) throw new UnauthorizedAccessException("无权限");
            var rev = await _eventRepository.CreateRuleRevisionAsync(new EventRuleRevision
            {
                EventId = eventId,
                ContentMarkdown = dto.ContentMarkdown,
                ChangeNotes = dto.ChangeNotes,
                CreatedByUserId = userId,
                CreatedAt = ChinaNow()
            });
            _logger?.LogInformation("CreateRuleRevision EventId={EventId} RevId={RevId} Version={Version}", eventId, rev.Id, rev.Version);
            return MapToRuleRevisionDto(rev);
        }

        public async Task<IEnumerable<RuleRevisionDto>> GetRuleRevisionsAsync(Guid eventId)
        {
            var list = await _eventRepository.GetRuleRevisionsAsync(eventId);
            return list.Select(MapToRuleRevisionDto);
        }

        public async Task<bool> PublishRuleRevisionAsync(Guid eventId, Guid revisionId, string userId)
        {
            if (!await CanUserManageEventAsync(eventId, userId)) throw new UnauthorizedAccessException("无权限");
            var ok = await _eventRepository.PublishRuleRevisionAsync(eventId, revisionId);
            if (ok) _logger?.LogInformation("PublishRuleRevision EventId={EventId} RevId={RevId}", eventId, revisionId);
            return ok;
        }

        // 报名表 Schema 与答案
        public async Task<bool> UpdateRegistrationFormSchemaAsync(Guid eventId, UpdateRegistrationFormSchemaDto dto, string userId)
        {
            if (!await CanUserManageEventAsync(eventId, userId)) throw new UnauthorizedAccessException("无权限");
            var ok = await _eventRepository.UpdateRegistrationFormSchemaAsync(eventId, dto.SchemaJson);
            if (ok) _logger?.LogInformation("UpdateRegistrationFormSchema EventId={EventId} Size={Size}", eventId, dto.SchemaJson?.Length);
            return ok;
        }

        public async Task<bool> SubmitRegistrationAnswersAsync(Guid eventId, SubmitRegistrationAnswersDto dto, string userId)
        {
            if (!await CanUserManageTeamRegistrationAsync(dto.TeamId, userId)) throw new UnauthorizedAccessException("无权限");
            var ans = await _eventRepository.UpsertRegistrationAnswersAsync(eventId, dto.TeamId, dto.AnswersJson, userId);
            _logger?.LogInformation("SubmitRegistrationAnswers EventId={EventId} TeamId={TeamId} Size={Size}", eventId, dto.TeamId, dto.AnswersJson?.Length);
            return ans != null;
        }

        public async Task<string> GetRegistrationFormSchemaAsync(Guid eventId)
        {
            var ev = await _eventRepository.GetEventByIdAsync(eventId);
            if (ev == null) throw new InvalidOperationException("赛事不存在");
            var current = string.IsNullOrWhiteSpace(ev.CustomData) ? "{}" : ev.CustomData;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(current);
                var root = doc.RootElement;
                if (root.TryGetProperty("registrationFormSchema", out var schema))
                {
                    if (schema.ValueKind == System.Text.Json.JsonValueKind.String)
                        return schema.GetString() ?? "{}";
                    return schema.GetRawText();
                }
            }
            catch { }
            return "{}";
        }

        public async Task<string> GetRegistrationAnswersAsync(Guid eventId, Guid teamId, string userId)
        {
            var can = await CanUserManageTeamRegistrationAsync(teamId, userId) || await CanUserManageEventAsync(eventId, userId);
            if (!can) throw new UnauthorizedAccessException("无权限");
            var ans = await _eventRepository.GetRegistrationAnswersAsync(eventId, teamId);
            return ans?.AnswersJson ?? "{}";
        }

        public async Task<bool> UpdateTournamentConfigAsync(Guid eventId, UpdateTournamentConfigDto dto, string userId)
        {
            if (!await CanUserManageEventAsync(eventId, userId)) throw new UnauthorizedAccessException("无权限");
            var ev = await _eventRepository.GetEventByIdAsync(eventId);
            if (ev == null) throw new InvalidOperationException("赛事不存在");
            System.Text.Json.Nodes.JsonObject node;
            try { node = (!string.IsNullOrWhiteSpace(ev.CustomData) ? System.Text.Json.Nodes.JsonNode.Parse(ev.CustomData) as System.Text.Json.Nodes.JsonObject : null) ?? new System.Text.Json.Nodes.JsonObject(); }
            catch { node = new System.Text.Json.Nodes.JsonObject(); }
            var json = System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
            node["tournamentConfig"] = System.Text.Json.Nodes.JsonNode.Parse(json);
            ev.CustomData = node.ToJsonString();
            await _eventRepository.UpdateEventAsync(ev);
            _logger?.LogInformation("UpdateTournamentConfig EventId={EventId} Format={Format}", eventId, dto.Format);
            return true;
        }

        public async Task<IEnumerable<TeamEventDto>> GenerateTestRegistrationsAsync(Guid eventId, int count, string userId, string? namePrefix = null, bool approve = true)
        {
            var ev = await _eventRepository.GetEventByIdAsync(eventId);
            if (ev == null) throw new InvalidOperationException("赛事不存在");
            if (!await CanUserManageEventAsync(eventId, userId)) throw new UnauthorizedAccessException("无权限");

            var now = ChinaNow();
            var created = new List<TeamEventDto>();
            var prefix = string.IsNullOrWhiteSpace(namePrefix) ? "测试战队" : namePrefix!.Trim();

            for (int i = 1; i <= count; i++)
            {
                var baseName = $"{prefix} {i.ToString("D3")}";
                var name = baseName;
                var suffix = 0;
                while (await _teamRepository.TeamNameExistsAsync(name))
                {
                    suffix++;
                    name = $"{baseName}-{suffix}";
                    if (suffix > 5) break;
                }

                var team = new Models.Team
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Password = BCrypt.Net.BCrypt.HashPassword("123456"),
                    Description = "测试生成",
                    QqNumber = ($"1{i:D3}000"),
                    OwnerId = userId,
                    UserId = userId,
                    Players = new List<Models.Player>
                    {
                        new Models.Player
                        {
                            Id = Guid.NewGuid(),
                            Name = $"选手{i:D3}",
                            GameId = $"G{i:D5}",
                            GameRank = "",
                            Description = "",
                        }
                    }
                };

                var createdTeam = await _teamRepository.CreateTeamAsync(team);
                var te = new Models.TeamEvent
                {
                    TeamId = createdTeam.Id,
                    EventId = eventId,
                    RegistrationTime = now,
                    Status = approve ? Models.RegistrationStatus.Approved : Models.RegistrationStatus.Pending,
                    Notes = "测试生成",
                    RegisteredByUserId = userId
                };
                var createdTe = await _eventRepository.RegisterTeamToEventAsync(te);
                created.Add(MapToTeamEventDto(createdTe, createdTeam.Name, ev.Name));
            }

            return created;
        }

        private static RuleRevisionDto MapToRuleRevisionDto(EventRuleRevision r)
        {
            return new RuleRevisionDto
            {
                Id = r.Id,
                EventId = r.EventId,
                Version = r.Version,
                ContentMarkdown = r.ContentMarkdown,
                ChangeNotes = r.ChangeNotes,
                CreatedByUserId = r.CreatedByUserId,
                CreatedAt = r.CreatedAt,
                IsPublished = r.IsPublished,
                PublishedAt = r.PublishedAt
            };
        }
    }
}
