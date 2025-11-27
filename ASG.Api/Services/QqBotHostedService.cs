using ASG.Api.Services.Bot;
using ASG.Api.DTOs;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ASG.Api.Services;
using ASG.Api.Data;
using ASG.Api.Repositories;
using BCrypt.Net;
using Microsoft.AspNetCore.Hosting;
using System.IO;
namespace ASG.Api.Services
{
    public class BotOptions
    {
        public string? WebSocketUrl { get; set; }
        public string? AccessToken { get; set; }
        public bool Enabled { get; set; } = false;
        public long[]? AllowedGroups { get; set; }
    }
    public class QqBotHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<BotOptions> _options;
        private OneBotClient? _client;
        public QqBotHostedService(IServiceScopeFactory scopeFactory, IOptions<BotOptions> options)
        {
            _scopeFactory = scopeFactory;
            _options = options;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var url = _options.Value.WebSocketUrl ?? "ws://127.0.0.1:8080/ws";
            var token = _options.Value.AccessToken;
            if (!_options.Value.Enabled) return;
            _client = new OneBotClient(url, token);
            _client.OnEvent += async node =>
            {
                try
                {
                    var postType = node?["post_type"]?.GetValue<string>();
                    if (postType == "message")
                    {
                        var msgType = node?["message_type"]?.GetValue<string>();
                        if (msgType == "group")
                        {
                            var gid = node?["group_id"]?.GetValue<long>() ?? 0;
                            if (_options.Value.AllowedGroups != null && _options.Value.AllowedGroups.Length > 0)
                            {
                                if (!_options.Value.AllowedGroups.Contains(gid)) return;
                            }
                            var uid = node?["user_id"]?.GetValue<long>() ?? 0;
                            var text = ExtractText(node);
                            var reply = await HandleCommandAsync(text, uid, stoppingToken);
                            if (!string.IsNullOrWhiteSpace(reply))
                                await _client!.SendGroupMessageAsync(gid, reply, stoppingToken);
                        }
                        else if (msgType == "private")
                        {
                            var uid = node?["user_id"]?.GetValue<long>() ?? 0;
                            var text = ExtractText(node);
                            var reply = await HandleCommandAsync(text, uid, stoppingToken);
                            if (!string.IsNullOrWhiteSpace(reply))
                                await _client!.SendPrivateMessageAsync(uid, reply, stoppingToken);
                        }
                    }
                }
                catch { }
            };
            try
            {
                await _client.ConnectAsync(stoppingToken);
            }
            catch { }
        }
        private static string ExtractText(JsonObject? node)
        {
            var msgNode = node?["message"];
            if (msgNode is JsonArray arr)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var seg in arr)
                {
                    var o = seg as JsonObject;
                    var t = o?["type"]?.GetValue<string>();
                    if (t == "text")
                    {
                        var d = o?["data"] as JsonObject;
                        var tx = d?["text"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(tx)) sb.Append(tx);
                    }
                }
                return sb.ToString().Trim();
            }
            if (msgNode is JsonValue v)
            {
                var s = v.GetValue<string>();
                return s ?? string.Empty;
            }
            return string.Empty;
        }
        private async Task<string> HandleCommandAsync(string raw, long senderQq, CancellationToken ct)
        {
            var text = raw.Trim();
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (Regex.IsMatch(text, "^(帮助|help)$", RegexOptions.IgnoreCase))
            {
                return "指令: 赛事 [关键词]|报名、\r\n赛程 [赛事ID|名称]\r\n绑定战队 Token\r\n报名 赛事ID\r\n我的战队\r\n我的赛程\r\n我的参赛";
            }
            if (Regex.IsMatch(text, "^赛事( .+)?$"))
            {
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                using var scope = _scopeFactory.CreateScope();
                var eventSvc = scope.ServiceProvider.GetRequiredService<IEventService>();
                if (parts.Length == 1)
                {
                    var list = await eventSvc.GetUpcomingEventsAsync();
                    var top = list.Take(5).ToList();
                    if (top.Count == 0) return "暂无即将开始的赛事";
                    var lines = top.Select(e => $"{e.Name} | 开赛 {e.CompetitionStartTime:yyyy-MM-dd HH:mm}");
                    return string.Join("\n", lines);
                }
                if (parts.Length >= 2 && parts[1] == "报名")
                {
                    var list = await eventSvc.GetActiveRegistrationEventsAsync();
                    var top = list.Take(5).ToList();
                    if (top.Count == 0) return "暂无开放报名的赛事";
                    var lines = top.Select(e => $"{e.Name} | 报名截止 {e.RegistrationEndTime:yyyy-MM-dd HH:mm}");
                    return string.Join("\n", lines);
                }
                var q = string.Join(' ', parts.Skip(1));
                var paged = await eventSvc.SearchEventsAsync(q, 1, 5);
                var items = paged.Items.ToList();
                if (!items.Any()) return "未找到相关赛事";
                var lines2 = items.Select(e => $"{e.Name} | ID {e.Id}");
                return string.Join("\n", lines2);
            }
            if (Regex.IsMatch(text, "^赛程( .+)?$"))
            {
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                using var scope = _scopeFactory.CreateScope();
                var eventSvc = scope.ServiceProvider.GetRequiredService<IEventService>();
                var matchSvc = scope.ServiceProvider.GetRequiredService<IMatchService>();
                Guid? eventId = null;
                if (parts.Length >= 2)
                {
                    if (Guid.TryParse(parts[1], out var gid)) eventId = gid;
                    else
                    {
                        var paged = await eventSvc.SearchEventsAsync(string.Join(' ', parts.Skip(1)), 1, 1);
                        var e1 = paged.Items.FirstOrDefault();
                        if (e1 != null) eventId = e1.Id;
                    }
                }
                var list = await matchSvc.GetAllMatchesAsync(eventId, 1, 10);
                var top = list.OrderBy(m => m.MatchTime).Take(5).ToList();
                if (top.Count == 0) return "暂无赛程";
                var lines = top.Select(m => $"{m.MatchTime:yyyy-MM-dd HH:mm} {m.HomeTeamName} vs {m.AwayTeamName}");
                return string.Join("\n", lines);
            }
            if (Regex.IsMatch(text, "^绑定战队( .+)?$"))
            {
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) return "用法: 绑定战队 Token";
                if (!Guid.TryParse(parts[1], out var token)) return "Token格式不正确";
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var team = db.Teams.FirstOrDefault(t => t.InviteToken == token);
                if (team == null) return "邀请不存在";
                var exp = team.InviteExpiresAt ?? DateTime.MinValue;
                if (DateTime.UtcNow > exp) return "邀请已过期";
                team.QqNumber = senderQq.ToString();
                team.InviteToken = null;
                team.InviteExpiresAt = null;
                await db.SaveChangesAsync(ct);
                return "绑定成功";
            }
            if (Regex.IsMatch(text, "^报名( .+)?$"))
            {
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) return "用法: 报名 赛事ID";
                if (!Guid.TryParse(parts[1], out var eventId)) return "赛事ID格式不正确";
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var team = db.Teams.FirstOrDefault(t => t.QqNumber == senderQq.ToString());
                if (team == null) return "未绑定战队";
                var ownerUserId = !string.IsNullOrEmpty(team.OwnerId) ? team.OwnerId : team.UserId;
                if (string.IsNullOrEmpty(ownerUserId)) return "战队缺少拥有者，请在网站绑定";
                var eventSvc = scope.ServiceProvider.GetRequiredService<IEventService>();
                try
                {
                    var dto = new RegisterTeamToEventDto { TeamId = team.Id, Notes = null };
                    var result = await eventSvc.RegisterTeamToEventAsync(eventId, dto, ownerUserId);
                    if (result == null) return "报名失败";
                    return $"报名成功：{result.EventName ?? parts[1]} - {team.Name}";
                }
                catch (UnauthorizedAccessException ex)
                {
                    return $"无权限：{ex.Message}";
                }
                catch (InvalidOperationException ex)
                {
                    return $"报名失败：{ex.Message}";
                }
                catch
                {
                    return "报名失败";
                }
            }
            if (Regex.IsMatch(text, "^我的参赛$"))
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var team = db.Teams.FirstOrDefault(t => t.QqNumber == senderQq.ToString());
                if (team == null) return "未绑定战队";
                var now = DateTime.UtcNow;
                var list = db.TeamEvents
                    .Where(te => te.TeamId == team.Id)
                    .Join(db.Events, te => te.EventId, e => e.Id, (te, e) => new { te, e })
                    .Where(x => x.e.CompetitionStartTime > now)
                    .OrderBy(x => x.e.CompetitionStartTime)
                    .Take(10)
                    .Select(x => new { x.e.Name, x.e.CompetitionStartTime })
                    .ToList();
                if (list.Count == 0) return "暂无未开始的参赛";
                var lines = list.Select(x => $"{x.Name} | 开赛 {x.CompetitionStartTime:yyyy-MM-dd HH:mm}");
                return string.Join("\n", lines);
            }
            if (Regex.IsMatch(text, "^我的战队$"))
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var team = db.Teams.FirstOrDefault(t => t.QqNumber == senderQq.ToString());
                if (team == null) return "未绑定战队";
                string logoUrl = string.Empty;
                string logoUrlAbs = string.Empty;
                try
                {
                    var env = scope.ServiceProvider.GetService<IWebHostEnvironment>();
                    var root = env?.WebRootPath;
                    if (string.IsNullOrEmpty(root)) root = env?.ContentRootPath != null ? Path.Combine(env.ContentRootPath, "wwwroot") : null;
                    if (!string.IsNullOrEmpty(root))
                    {
                        var dir = Path.Combine(root, "team-logos", team.Id.ToString());
                        if (Directory.Exists(dir))
                        {
                            var files = Directory.GetFiles(dir, "logo.*");
                            if (files.Length > 0)
                            {
                                var fileName = Path.GetFileName(files[0]);
                                logoUrl = $"/team-logos/{team.Id}/{fileName}";
                                try
                                {
                                    var config = scope.ServiceProvider.GetService<IConfiguration>();
                                    var envUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
                                    var urlsCfg = config?["Urls"];
                                    var baseUrl = (!string.IsNullOrEmpty(envUrls) ? envUrls : urlsCfg)?.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                                    if (string.IsNullOrEmpty(baseUrl))
                                    {
                                        var envName = env?.EnvironmentName ?? "";
                                        baseUrl = envName.Equals("Development", StringComparison.OrdinalIgnoreCase) ? "http://localhost:5250" : "https://api.idvevent.cn";
                                    }
                                    logoUrlAbs = $"{baseUrl?.TrimEnd('/')}{logoUrl}";
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch { }
                var names = db.Players.Where(p => p.TeamId == team.Id).Select(p => p.Name).ToList();
                var s = new List<string>();
                s.Add($"战队：{team.Name}");
               s.Add($"队员：{(names.Count > 0 ? string.Join("、", names) : "无")}");
                if (!string.IsNullOrEmpty(logoUrlAbs))
                {
                    s.Add($"[CQ:image,url={logoUrlAbs}]");
                }
                return string.Join("\n", s);
            }
            if (Regex.IsMatch(text, "^我的赛程$"))
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var team = db.Teams.FirstOrDefault(t => t.QqNumber == senderQq.ToString());
                if (team == null) return "未绑定战队";
                var ms = db.Matches.Where(m => m.HomeTeamId == team.Id || m.AwayTeamId == team.Id)
                    .OrderBy(m => m.MatchTime).Take(5).ToList();
                if (ms.Count == 0) return "暂无赛程";
                var list = new List<string>();
                foreach (var m in ms)
                {
                    var homeName = db.Teams.Where(t => t.Id == m.HomeTeamId).Select(t => t.Name).FirstOrDefault() ?? string.Empty;
                    var awayName = db.Teams.Where(t => t.Id == m.AwayTeamId).Select(t => t.Name).FirstOrDefault() ?? string.Empty;
                    var role = m.HomeTeamId == team.Id ? "主" : "客";
                    list.Add($"{m.MatchTime:yyyy-MM-dd HH:mm} {role} {homeName} vs {awayName}");
                }
                return string.Join("\n", list);
            }
            return string.Empty;
        }
    }
}
