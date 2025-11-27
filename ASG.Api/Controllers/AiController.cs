using ASG.Api.DTOs;
using ASG.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AiController : ControllerBase
    {
        private readonly IAiService _ai;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ITeamService _teamService;
        private readonly IEventService _eventService;

        public AiController(IAiService ai, IWebHostEnvironment env, IConfiguration config, ITeamService teamService, IEventService eventService)
        {
            _ai = ai;
            _env = env;
            _config = config;
            _teamService = teamService;
            _eventService = eventService;
        }

        [HttpPost("generate-logo")]
        public async Task<IActionResult> GenerateLogo([FromBody] GenerateLogoRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Name) || string.IsNullOrWhiteSpace(dto?.Description))
                return BadRequest(new { message = "需要名称与简介" });

            var data = await _ai.GenerateLogoAsync(dto.Name.Trim(), dto.Description.Trim(), HttpContext.RequestAborted);
            if (data == null || data.Length == 0)
                return StatusCode(502, new { message = "生成失败" });

            var root = _env.WebRootPath;
            if (string.IsNullOrEmpty(root)) root = Path.Combine(_env.ContentRootPath, "wwwroot");
            var dir = Path.Combine(root, "uploads", "ai");
            Directory.CreateDirectory(dir);
            var fileName = $"{Guid.NewGuid():N}.png";
            var filePath = Path.Combine(dir, fileName);
            await System.IO.File.WriteAllBytesAsync(filePath, data, HttpContext.RequestAborted);
            var relativePath = $"/uploads/ai/{fileName}";
            var scheme = Request.Scheme;
            var host = Request.Host.HasValue ? Request.Host.Value : string.Empty;
            var url = !string.IsNullOrEmpty(host) ? $"{scheme}://{host}{relativePath}" : relativePath;
            return Ok(new GenerateLogoResponseDto { Url = url });
        }

        [HttpPost("polish-text")]
        public async Task<IActionResult> PolishText([FromBody] PolishTextRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Text)) return BadRequest(new { message = "缺少文本" });
            var outText = await _ai.PolishTextAsync(dto.Scope?.Trim() ?? "general", dto.Text.Trim(), HttpContext.RequestAborted);
            if (string.IsNullOrWhiteSpace(outText)) return StatusCode(502, new { message = "处理失败" });
            return Ok(new PolishTextResponseDto { Text = outText });
        }

        [HttpPost("command")]
        public async Task<IActionResult> Command([FromBody] AiCommandRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Command)) return BadRequest(new { message = "缺少指令" });
            var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var results = new List<AiCommandActionResult>();
            var maxRounds = 3;
            for (var round = 0; round < maxRounds; round++)
            {
                var toolCalls = await _ai.PlanToolsAsync(dto.Command.Trim(), results, HttpContext.RequestAborted);
                if (toolCalls == null || toolCalls.Count == 0) break;
                var hasNonLlm = toolCalls.Any(c => !string.Equals(c.Name?.Trim(), "llm_response", StringComparison.OrdinalIgnoreCase));
                foreach (var call in toolCalls)
                {
                    var name = call.Name?.Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (results.Any(r => string.Equals(r.Action, name, StringComparison.OrdinalIgnoreCase) && r.Success)) continue;
                    var args = call.Arguments;
                    try
                    {
                        if (name == "polish_text")
                        {
                            var scope = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("scope", out var s) ? s.GetString() ?? "general" : "general";
                            var inputText = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                            var outText = await _ai.PolishTextAsync(scope, inputText, HttpContext.RequestAborted);
                            results.Add(new AiCommandActionResult { Action = name, Success = !string.IsNullOrWhiteSpace(outText), Data = new { text = outText } });
                        }
                        else if (name == "bind_team_by_name")
                        {
                            var nm = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("name", out var nmProp) ? nmProp.GetString() ?? string.Empty : string.Empty;
                            var pwd = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("password", out var pwProp) ? pwProp.GetString() ?? string.Empty : string.Empty;
                            if (string.IsNullOrWhiteSpace(nm) || string.IsNullOrWhiteSpace(pwd))
                            {
                                results.Add(new AiCommandActionResult { Action = "request_user_input", Success = false, Message = "缺少参数", Data = new { original_action = "bind_team_by_name", fields = new[] { new { name = "name", label = "战队名称", example = "帅哥战队", required = true }, new { name = "password", label = "密码", example = "******", required = true } } } });
                                continue;
                            }
                            var ok = !string.IsNullOrEmpty(userId) && await _teamService.BindTeamByNameAsync(nm, pwd, userId);
                            results.Add(new AiCommandActionResult { Action = name, Success = ok });
                        }
                        else if (name == "like_team_by_name")
                        {
                            var tn = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("team_name", out var tnProp) ? tnProp.GetString() ?? string.Empty : string.Empty;
                            if (string.IsNullOrWhiteSpace(tn))
                            {
                                results.Add(new AiCommandActionResult { Action = "request_user_input", Success = false, Message = "缺少参数", Data = new { original_action = "like_team_by_name", fields = new[] { new { name = "team_name", label = "战队名称", example = "帅哥战队", required = true } } } });
                                continue;
                            }
                            var search = await _teamService.SearchTeamsByNameAsync(tn, 1, 1);
                            var team = search.Items.FirstOrDefault();
                            if (team != null)
                            {
                                var likes = await _teamService.LikeTeamAsync(team.Id);
                                results.Add(new AiCommandActionResult { Action = name, Success = true, Data = new { teamId = team.Id, teamName = team.Name, likes } });
                            }
                            else
                            {
                                results.Add(new AiCommandActionResult { Action = name, Success = false, Message = "未找到战队" });
                            }
                        }
                        else if (name == "register_team_to_event_by_names")
                        {
                            var tn = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("team_name", out var tnProp) ? tnProp.GetString() ?? string.Empty : string.Empty;
                            var en = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("event_name", out var enProp) ? enProp.GetString() ?? string.Empty : string.Empty;
                            var notes = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("notes", out var noProp) ? noProp.GetString() : null;
                            if (string.IsNullOrWhiteSpace(tn) || string.IsNullOrWhiteSpace(en))
                            {
                                results.Add(new AiCommandActionResult { Action = "request_user_input", Success = false, Message = "缺少参数", Data = new { original_action = "register_team_to_event_by_names", fields = new[] { new { name = "team_name", label = "战队名称", example = "帅哥战队", required = true }, new { name = "event_name", label = "赛事名称", example = "冬季赛", required = true }, new { name = "notes", label = "报名备注", example = "祝好运", required = false } } } });
                                continue;
                            }
                            var tRes = await _teamService.SearchTeamsByNameAsync(tn, 1, 1);
                            var team = tRes.Items.FirstOrDefault();
                            var eRes = await _eventService.SearchEventsAsync(en, 1, 1);
                            var evt = eRes.Items.FirstOrDefault();
                            if (team != null && evt != null && !string.IsNullOrEmpty(userId))
                            {
                                var dtoReg = new RegisterTeamToEventDto { TeamId = team.Id, Notes = notes };
                                var reg = await _eventService.RegisterTeamToEventAsync(evt.Id, dtoReg, userId);
                                results.Add(new AiCommandActionResult { Action = name, Success = reg != null, Data = reg });
                            }
                            else
                            {
                                results.Add(new AiCommandActionResult { Action = name, Success = false, Message = "未找到战队或赛事，或未登录" });
                            }
                        }
                        else if (name == "search_teams")
                        {
                            var q = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("query", out var qProp) ? qProp.GetString() ?? string.Empty : string.Empty;
                            if (string.IsNullOrWhiteSpace(q))
                            {
                                results.Add(new AiCommandActionResult { Action = "request_user_input", Success = false, Message = "缺少参数", Data = new { original_action = "search_teams", fields = new[] { new { name = "query", label = "搜索关键词", example = "帅哥", required = true } } } });
                                continue;
                            }
                            var page = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("page", out var pProp) && pProp.ValueKind == JsonValueKind.Number ? pProp.GetInt32() : 1;
                            var pageSize = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("page_size", out var psProp) && psProp.ValueKind == JsonValueKind.Number ? psProp.GetInt32() : 10;
                            var res = await _teamService.SearchTeamsByNameAsync(q, page, pageSize);
                            results.Add(new AiCommandActionResult { Action = name, Success = true, Data = res });
                        }
                        else if (name == "get_team_by_name")
                        {
                            var nm = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("name", out var nmProp) ? nmProp.GetString() ?? string.Empty : string.Empty;
                            if (string.IsNullOrWhiteSpace(nm))
                            {
                                results.Add(new AiCommandActionResult { Action = "request_user_input", Success = false, Message = "缺少参数", Data = new { original_action = "get_team_by_name", fields = new[] { new { name = "name", label = "战队名称", example = "帅哥战队", required = true } } } });
                                continue;
                            }
                            var res = await _teamService.SearchTeamsByNameAsync(nm, 1, 1);
                            var team = res.Items.FirstOrDefault();
                            results.Add(new AiCommandActionResult { Action = name, Success = team != null, Data = team });
                        }
                        else if (name == "get_team_honors_by_name")
                        {
                            var tn = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("team_name", out var tnProp) ? tnProp.GetString() ?? string.Empty : string.Empty;
                            if (string.IsNullOrWhiteSpace(tn))
                            {
                                results.Add(new AiCommandActionResult { Action = "request_user_input", Success = false, Message = "缺少参数", Data = new { original_action = "get_team_honors_by_name", fields = new[] { new { name = "team_name", label = "战队名称", example = "帅哥战队", required = true } } } });
                                continue;
                            }
                            var res = await _teamService.SearchTeamsByNameAsync(tn, 1, 1);
                            var team = res.Items.FirstOrDefault();
                            if (team != null)
                            {
                                var honors = await _eventService.GetChampionEventsByTeamAsync(team.Id);
                                results.Add(new AiCommandActionResult { Action = name, Success = true, Data = honors });
                            }
                            else
                            {
                                results.Add(new AiCommandActionResult { Action = name, Success = false, Message = "未找到战队" });
                            }
                        }
                        else if (name == "search_events")
                        {
                            var q = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("query", out var qProp) ? qProp.GetString() ?? string.Empty : string.Empty;
                            if (string.IsNullOrWhiteSpace(q))
                            {
                                results.Add(new AiCommandActionResult { Action = "request_user_input", Success = false, Message = "缺少参数", Data = new { original_action = "search_events", fields = new[] { new { name = "query", label = "搜索关键词", example = "冬季赛", required = true } } } });
                                continue;
                            }
                            var page = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("page", out var pProp) && pProp.ValueKind == JsonValueKind.Number ? pProp.GetInt32() : 1;
                            var pageSize = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("page_size", out var psProp) && psProp.ValueKind == JsonValueKind.Number ? psProp.GetInt32() : 12;
                            var res = await _eventService.SearchEventsAsync(q, page, pageSize);
                            results.Add(new AiCommandActionResult { Action = name, Success = true, Data = res });
                        }
                        else if (name == "list_active_events")
                        {
                            var page = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("page", out var pProp) && pProp.ValueKind == JsonValueKind.Number ? pProp.GetInt32() : 1;
                            var pageSize = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("page_size", out var psProp) && psProp.ValueKind == JsonValueKind.Number ? psProp.GetInt32() : 12;
                            var res = await _eventService.GetActiveRegistrationEventsAsync(page, pageSize);
                            results.Add(new AiCommandActionResult { Action = name, Success = true, Data = res });
                        }
                        else if (name == "list_upcoming_events")
                        {
                            var page = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("page", out var pProp) && pProp.ValueKind == JsonValueKind.Number ? pProp.GetInt32() : 1;
                            var pageSize = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("page_size", out var psProp) && psProp.ValueKind == JsonValueKind.Number ? psProp.GetInt32() : 12;
                            var res = await _eventService.GetUpcomingEventsAsync(page, pageSize);
                            results.Add(new AiCommandActionResult { Action = name, Success = true, Data = res });
                        }
                        else if (name == "get_event_registrations_by_name")
                        {
                            var en = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("event_name", out var enProp) ? enProp.GetString() ?? string.Empty : string.Empty;
                            if (string.IsNullOrWhiteSpace(en))
                            {
                                results.Add(new AiCommandActionResult { Action = "request_user_input", Success = false, Message = "缺少参数", Data = new { original_action = "get_event_registrations_by_name", fields = new[] { new { name = "event_name", label = "赛事名称", example = "冬季赛", required = true } } } });
                                continue;
                            }
                            var eRes = await _eventService.SearchEventsAsync(en, 1, 1);
                            var evt = eRes.Items.FirstOrDefault();
                            if (evt != null)
                            {
                                var regs = await _eventService.GetEventRegistrationsWithSensitiveAsync(evt.Id, string.IsNullOrEmpty(userId) ? null : userId);
                                results.Add(new AiCommandActionResult { Action = name, Success = true, Data = regs });
                            }
                            else
                            {
                                results.Add(new AiCommandActionResult { Action = name, Success = false, Message = "未找到赛事" });
                            }
                        }
                        else if (name == "get_my_events")
                        {
                            if (!string.IsNullOrEmpty(userId))
                            {
                                var my = await _eventService.GetEventsByCreatorAsync(userId);
                                results.Add(new AiCommandActionResult { Action = name, Success = true, Data = my });
                            }
                            else
                            {
                                results.Add(new AiCommandActionResult { Action = name, Success = false, Message = "未登录" });
                            }
                        }
                        else if (name == "get_my_player")
                        {
                            if (!string.IsNullOrEmpty(userId))
                            {
                                var me = await _teamService.GetMyPlayerAsync(userId);
                                results.Add(new AiCommandActionResult { Action = name, Success = me != null, Data = me });
                            }
                            else
                            {
                                results.Add(new AiCommandActionResult { Action = name, Success = false, Message = "未登录" });
                            }
                        }
                        else if (name == "llm_response")
                        {
                            var llmText = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                            results.Add(new AiCommandActionResult { Action = name, Success = !string.IsNullOrWhiteSpace(llmText), Data = new { text = llmText } });
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new AiCommandActionResult { Action = name, Success = false, Message = ex.Message });
                    }
                }
                if (!hasNonLlm && round > 0) break;
                var cmdText = dto.Command.Trim();
                var llm = results.FirstOrDefault(r => string.Equals(r.Action, "llm_response", StringComparison.OrdinalIgnoreCase));
                try
                {
                    if (llm?.Data != null)
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(llm.Data);
                        using var jd = System.Text.Json.JsonDocument.Parse(json);
                        if (jd.RootElement.TryGetProperty("text", out var t))
                        {
                            var s = t.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) cmdText = s.Trim();
                        }
                    }
                }
                catch { }
                var lower = cmdText.ToLowerInvariant();
                try
                {
                    if (!results.Any(r => string.Equals(r.Action, "like_team_by_name", StringComparison.OrdinalIgnoreCase)) && lower.Contains("赞"))
                    {
                        string? tn = null;
                        var qStart = cmdText.IndexOf('"');
                        var qEnd = qStart >= 0 ? cmdText.IndexOf('"', qStart + 1) : -1;
                        if (qStart >= 0 && qEnd > qStart) tn = cmdText.Substring(qStart + 1, qEnd - qStart - 1);
                        if (string.IsNullOrWhiteSpace(tn))
                        {
                            var key = "战队";
                            var idx = cmdText.IndexOf(key);
                            if (idx >= 0)
                            {
                                var rest = cmdText.Substring(idx + key.Length).Trim();
                                var stopIdx = rest.IndexOf(' ');
                                if (stopIdx > 0) tn = rest.Substring(0, stopIdx).Trim(); else tn = rest.Trim();
                                tn = tn.Replace("点", string.Empty).Replace("点赞", string.Empty).Trim();
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(tn))
                        {
                            var search = await _teamService.SearchTeamsByNameAsync(tn, 1, 1);
                            var team = search.Items.FirstOrDefault();
                            if (team != null)
                            {
                                var likes = await _teamService.LikeTeamAsync(team.Id);
                                results.Add(new AiCommandActionResult { Action = "like_team_by_name", Success = true, Data = new { teamId = team.Id, teamName = team.Name, likes } });
                            }
                        }
                        else
                        {
                            results.Add(new AiCommandActionResult { Action = "request_user_input", Success = false, Message = "缺少参数", Data = new { original_action = "like_team_by_name", fields = new[] { new { name = "team_name", label = "战队名称", example = "帅哥战队", required = true } } } });
                        }
                    }
                    if (!results.Any(r => string.Equals(r.Action, "register_team_to_event_by_names", StringComparison.OrdinalIgnoreCase)) && lower.Contains("报名") && lower.Contains("赛事"))
                    {
                        string? tn = null; string? en = null; string? notes = null;
                        var parts = cmdText.Split(new[] { '，', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var p in parts)
                        {
                            var lp = p.ToLowerInvariant();
                            if (tn == null && lp.Contains("战队")) tn = p.Replace("战队", string.Empty).Replace("报名", string.Empty).Trim();
                            if (en == null && lp.Contains("赛事")) en = p.Replace("赛事", string.Empty).Replace("报名", string.Empty).Trim();
                            if (notes == null && lp.Contains("备注")) notes = p.Replace("备注", string.Empty).Trim();
                        }
                        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrWhiteSpace(tn) && !string.IsNullOrWhiteSpace(en))
                        {
                            var tRes = await _teamService.SearchTeamsByNameAsync(tn, 1, 1);
                            var team = tRes.Items.FirstOrDefault();
                            var eRes = await _eventService.SearchEventsAsync(en, 1, 1);
                            var evt = eRes.Items.FirstOrDefault();
                            if (team != null && evt != null)
                            {
                                var dtoReg = new RegisterTeamToEventDto { TeamId = team.Id, Notes = notes };
                                var reg = await _eventService.RegisterTeamToEventAsync(evt.Id, dtoReg, userId);
                                results.Add(new AiCommandActionResult { Action = "register_team_to_event_by_names", Success = reg != null, Data = reg });
                            }
                        }
                        else
                        {
                            results.Add(new AiCommandActionResult { Action = "request_user_input", Success = false, Message = "缺少参数", Data = new { original_action = "register_team_to_event_by_names", fields = new[] { new { name = "team_name", label = "战队名称", example = "帅哥战队", required = true }, new { name = "event_name", label = "赛事名称", example = "冬季赛", required = true }, new { name = "notes", label = "报名备注", example = "祝好运", required = false } } } });
                        }
                    }
                    if (!results.Any(r => string.Equals(r.Action, "bind_team_by_name", StringComparison.OrdinalIgnoreCase)) && lower.Contains("绑定") && lower.Contains("战队"))
                    {
                        string? tn = null; string? pwd = null;
                        var qStart = cmdText.IndexOf('"');
                        var qEnd = qStart >= 0 ? cmdText.IndexOf('"', qStart + 1) : -1;
                        if (qStart >= 0 && qEnd > qStart) tn = cmdText.Substring(qStart + 1, qEnd - qStart - 1);
                        var pwIdx = lower.IndexOf("密码");
                        if (pwIdx >= 0)
                        {
                            pwd = cmdText.Substring(pwIdx + 2).Trim();
                        }
                        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrWhiteSpace(tn) && !string.IsNullOrWhiteSpace(pwd))
                        {
                            var ok = await _teamService.BindTeamByNameAsync(tn, pwd, userId);
                            results.Add(new AiCommandActionResult { Action = "bind_team_by_name", Success = ok });
                        }
                        else
                        {
                            results.Add(new AiCommandActionResult { Action = "request_user_input", Success = false, Message = "缺少参数", Data = new { original_action = "bind_team_by_name", fields = new[] { new { name = "name", label = "战队名称", example = "帅哥战队", required = true }, new { name = "password", label = "密码", example = "******", required = true } } } });
                        }
                    }
                }
                catch
                {
                }
            }
            return Ok(new AiCommandResponseDto { Results = results });
        }
    }
}
