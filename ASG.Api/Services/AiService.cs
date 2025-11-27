using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ASG.Api.DTOs;

namespace ASG.Api.Services
{
    public class AiService : IAiService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;

        public AiService(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _httpFactory = httpFactory;
            _config = config;
        }

        public async Task<byte[]?> GenerateLogoAsync(string name, string description, CancellationToken ct)
        {
            var endpoint = _config["AI:SeeDream:Endpoint"] ?? _config["AI:SeeDreamEndpoint"] ?? "https://ark.cn-beijing.volces.com/api/v3/images/generations";
            var apiKey = _config["AI:SeeDream:ApiKey"] ?? _config["AI:ApiKey"];
            var model = _config["AI:SeeDream:Model"] ?? "doubao-seedream-4-0-250828";
            var size = _config["AI:SeeDream:Size"] ?? "2K";
            var responseFormat = _config["AI:SeeDream:ResponseFormat"] ?? "url";
            var watermarkRaw = _config["AI:SeeDream:Watermark"];
            var streamRaw = _config["AI:SeeDream:Stream"];
            var watermark = bool.TryParse(watermarkRaw, out var w) ? w : true;
            var stream = bool.TryParse(streamRaw, out var s) ? s : false;
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey)) return null;
            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var prompt = $"为战队{name}设计一个简洁现代的徽标风格logo。简介：{description}";
            var payload = new
            {
                model = model,
                prompt = prompt,
                sequential_image_generation = "auto",
                sequential_image_generation_options = new { max_images = 1 },
                response_format = responseFormat,
                size = size,
                stream = stream,
                watermark = watermark
            };
            var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            using var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    if (item.TryGetProperty("url", out var u))
                    {
                        var url = u.GetString();
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            using var imgResp = await client.GetAsync(url, ct);
                            if (imgResp.IsSuccessStatusCode)
                            {
                                return await imgResp.Content.ReadAsByteArrayAsync(ct);
                            }
                        }
                    }
                    if (item.TryGetProperty("image_base64", out var ib64))
                    {
                        var s64 = ib64.GetString();
                        if (!string.IsNullOrWhiteSpace(s64))
                        {
                            try { return Convert.FromBase64String(s64); } catch { }
                        }
                    }
                }
            }
            if (doc.RootElement.TryGetProperty("image_base64", out var b64))
            {
                try { return Convert.FromBase64String(b64.GetString() ?? ""); } catch { }
            }
            if (doc.RootElement.TryGetProperty("image_url", out var urlProp))
            {
                var url = urlProp.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    using var imgResp = await client.GetAsync(url, ct);
                    if (imgResp.IsSuccessStatusCode)
                    {
                        return await imgResp.Content.ReadAsByteArrayAsync(ct);
                    }
                }
            }
            return null;
        }

        public async Task<string?> PolishTextAsync(string scope, string text, CancellationToken ct)
        {
            var endpoint = _config["AI:Deepseek:Endpoint"] ?? _config["AI:PolishEndpoint"] ?? _config["AI:SeeDreamTextEndpoint"];
            var apiKey = _config["AI:Deepseek:ApiKey"] ?? _config["AI:ApiKey"];
            var model = _config["AI:Deepseek:Model"] ?? "deepseek-chat";
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey)) return null;
            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var payload = new
            {
                model = model,
                messages = new object[]
                {
                    new { role = "system", content = "请润色以下中文简介，保持原意，提升可读性。" },
                    new { role = "user", content = text }
                },
                temperature = 0.5
            };
            var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            using var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                var first = choices.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object)
                {
                    if (first.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object)
                    {
                        if (msg.TryGetProperty("content", out var content)) return content.GetString();
                    }
                    if (first.TryGetProperty("text", out var txt)) return txt.GetString();
                }
            }
            if (doc.RootElement.TryGetProperty("output", out var outProp)) return outProp.GetString();
            if (doc.RootElement.TryGetProperty("text", out var textProp)) return textProp.GetString();
            return null;
        }

        public class ToolCall
        {
            public string Name { get; set; } = string.Empty;
            public JsonElement Arguments { get; set; }
        }

        public async Task<List<ToolCall>?> PlanToolsAsync(string command, CancellationToken ct)
        {
            return await PlanToolsAsync(command, new List<AiCommandActionResult>(), ct);
        }

        public async Task<List<ToolCall>?> PlanToolsAsync(string command, List<AiCommandActionResult> previousResults, CancellationToken ct)
        {
            var endpoint = _config["AI:Deepseek:Endpoint"] ?? _config["AI:PolishEndpoint"] ?? _config["AI:SeeDreamTextEndpoint"];
            var apiKey = _config["AI:Deepseek:ApiKey"] ?? _config["AI:ApiKey"];
            var model = _config["AI:Deepseek:Model"] ?? "deepseek-chat";
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey)) return null;
            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var tools = new object[]
            {
                new { type = "function", function = new { name = "polish_text", description = "润色中文文本", parameters = new { type = "object", properties = new { scope = new { type = "string" }, text = new { type = "string" } }, required = new [] { "text" } } } },
                new { type = "function", function = new { name = "request_user_input", description = "当参数不完整时请求用户补充", parameters = new { type = "object", properties = new { original_action = new { type = "string" }, fields = new { type = "array", items = new { type = "object", properties = new { name = new { type = "string" }, label = new { type = "string" }, example = new { type = "string" }, required = new { type = "boolean" } }, required = new [] { "name" } } } }, required = new [] { "original_action", "fields" } } } },
                new { type = "function", function = new { name = "bind_team_by_name", description = "绑定战队", parameters = new { type = "object", properties = new { name = new { type = "string" }, password = new { type = "string" } }, required = new [] { "name", "password" } } } },
                new { type = "function", function = new { name = "like_team_by_name", description = "为战队点赞", parameters = new { type = "object", properties = new { team_name = new { type = "string" } }, required = new [] { "team_name" } } } },
                new { type = "function", function = new { name = "register_team_to_event_by_names", description = "战队报名赛事", parameters = new { type = "object", properties = new { team_name = new { type = "string" }, event_name = new { type = "string" }, notes = new { type = "string" } }, required = new [] { "team_name", "event_name" } } } },
                new { type = "function", function = new { name = "search_teams", description = "搜索战队", parameters = new { type = "object", properties = new { query = new { type = "string" }, page = new { type = "integer" }, page_size = new { type = "integer" } }, required = new [] { "query" } } } },
                new { type = "function", function = new { name = "get_team_by_name", description = "按名称获取战队", parameters = new { type = "object", properties = new { name = new { type = "string" } }, required = new [] { "name" } } } },
                new { type = "function", function = new { name = "get_team_honors_by_name", description = "查看战队荣誉", parameters = new { type = "object", properties = new { team_name = new { type = "string" } }, required = new [] { "team_name" } } } },
                new { type = "function", function = new { name = "search_events", description = "搜索赛事", parameters = new { type = "object", properties = new { query = new { type = "string" }, page = new { type = "integer" }, page_size = new { type = "integer" } }, required = new [] { "query" } } } },
                new { type = "function", function = new { name = "list_active_events", description = "正在报名的赛事", parameters = new { type = "object", properties = new { page = new { type = "integer" }, page_size = new { type = "integer" } }, required = new string[] { } } } },
                new { type = "function", function = new { name = "list_upcoming_events", description = "即将开始的赛事", parameters = new { type = "object", properties = new { page = new { type = "integer" }, page_size = new { type = "integer" } }, required = new string[] { } } } },
                new { type = "function", function = new { name = "get_event_registrations_by_name", description = "查看赛事报名列表", parameters = new { type = "object", properties = new { event_name = new { type = "string" } }, required = new [] { "event_name" } } } },
                new { type = "function", function = new { name = "get_my_events", description = "查看我创建的赛事", parameters = new { type = "object", properties = new { }, required = new string[] { } } } },
                new { type = "function", function = new { name = "get_my_player", description = "查看我的玩家信息", parameters = new { type = "object", properties = new { }, required = new string[] { } } } }
            };
            var messages = new List<object>();
            messages.Add(new { role = "system", content = "你是一个后端操作规划器。支持多轮迭代：根据上一轮的执行结果继续规划下一轮函数调用。若用户意图包含查询+执行或多个目标，必须一次性返回包含所有步骤的多个 tool_calls，并按合理顺序组织（如：先搜索实体，再执行点赞/报名/绑定等操作）。只调用提供的函数，按名称匹配实体。" });
            if (previousResults != null && previousResults.Count > 0)
            {
                var prevJson = JsonSerializer.Serialize(new { previous_results = previousResults });
                messages.Add(new { role = "assistant", content = prevJson });
            }
            messages.Add(new { role = "user", content = command });
            var payload = new
            {
                model = model,
                messages = messages.ToArray(),
                tools = tools,
                tool_choice = "auto",
                temperature = 0
            };
            var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            using var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var result = new List<ToolCall>();
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                var first = choices.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object)
                {
                    if (first.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object)
                    {
                        if (msg.TryGetProperty("tool_calls", out var tc) && tc.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var x in tc.EnumerateArray())
                            {
                                if (x.ValueKind != JsonValueKind.Object) continue;
                                if (x.TryGetProperty("function", out var fn) && fn.ValueKind == JsonValueKind.Object)
                                {
                                    var name = fn.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                                    var argsStr = fn.TryGetProperty("arguments", out var a) ? a.GetString() ?? "{}" : "{}";
                                    try
                                    {
                                        using var aDoc = JsonDocument.Parse(argsStr);
                                        result.Add(new ToolCall { Name = name, Arguments = aDoc.RootElement.Clone() });
                                    }
                                    catch
                                    {
                                        result.Add(new ToolCall { Name = name, Arguments = default });
                                    }
                                }
                            }
                        }
                        else if (msg.TryGetProperty("function_call", out var fc) && fc.ValueKind == JsonValueKind.Object)
                        {
                            var name = fc.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                            var argsStr = fc.TryGetProperty("arguments", out var a) ? a.GetString() ?? "{}" : "{}";
                            try
                            {
                                using var aDoc = JsonDocument.Parse(argsStr);
                                result.Add(new ToolCall { Name = name, Arguments = aDoc.RootElement.Clone() });
                            }
                            catch
                            {
                                result.Add(new ToolCall { Name = name, Arguments = default });
                            }
                        }
                        else if (msg.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                        {
                            var text = content.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                var argsJson = JsonSerializer.Serialize(new { text });
                                using var aDoc = JsonDocument.Parse(argsJson);
                                result.Add(new ToolCall { Name = "llm_response", Arguments = aDoc.RootElement.Clone() });
                            }
                        }
                        else if (msg.TryGetProperty("content", out var contentArr) && contentArr.ValueKind == JsonValueKind.Array)
                        {
                            var sb = new System.Text.StringBuilder();
                            foreach (var c in contentArr.EnumerateArray())
                            {
                                if (c.ValueKind == JsonValueKind.Object)
                                {
                                    if (c.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String)
                                    {
                                        var type = t.GetString() ?? string.Empty;
                                        if (type.Equals("text", StringComparison.OrdinalIgnoreCase) || type.Equals("output_text", StringComparison.OrdinalIgnoreCase))
                                        {
                                            var txt = c.TryGetProperty("text", out var tx) ? tx.GetString() ?? string.Empty : string.Empty;
                                            if (!string.IsNullOrWhiteSpace(txt)) sb.AppendLine(txt);
                                        }
                                    }
                                }
                            }
                            var final = sb.ToString().Trim();
                            if (!string.IsNullOrWhiteSpace(final))
                            {
                                var argsJson = JsonSerializer.Serialize(new { text = final });
                                using var aDoc = JsonDocument.Parse(argsJson);
                                result.Add(new ToolCall { Name = "llm_response", Arguments = aDoc.RootElement.Clone() });
                            }
                        }
                    }
                }
            }
            if (result.Count <= 1)
            {
                var strictSystem = "你是后端操作规划器。将用户意图转化为动作列表。只返回 JSON。若任务包含多个步骤（如查询+执行或多个目标），必须在同一响应中返回多条 actions，并按执行顺序组织。若参数不完整，使用 action 'request_user_input' 要求用户补充。JSON 格式: {\"actions\":[{\"name\":\"polish_text|request_user_input|bind_team_by_name|like_team_by_name|register_team_to_event_by_names|search_teams|get_team_by_name|get_team_honors_by_name|search_events|list_active_events|list_upcoming_events|get_event_registrations_by_name|get_my_events|get_my_player\",\"args\":{...}}]}。不要输出除 JSON 外的任何内容。";
                var messages2 = new List<object>();
                messages2.Add(new { role = "system", content = strictSystem });
                if (previousResults != null && previousResults.Count > 0)
                {
                    var prevJson2 = JsonSerializer.Serialize(new { previous_results = previousResults });
                    messages2.Add(new { role = "assistant", content = prevJson2 });
                }
                messages2.Add(new { role = "user", content = command });
                var payload2 = new
                {
                    model = model,
                    messages = messages2.ToArray(),
                    temperature = 0
                };
                var req2 = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload2), Encoding.UTF8, "application/json")
                };
                using var resp2 = await client.SendAsync(req2, ct);
                if (resp2.IsSuccessStatusCode)
                {
                    var json2 = await resp2.Content.ReadAsStringAsync(ct);
                    using var doc2 = JsonDocument.Parse(json2);
                    string contentText = string.Empty;
                    if (doc2.RootElement.TryGetProperty("choices", out var choices2) && choices2.ValueKind == JsonValueKind.Array)
                    {
                        var first2 = choices2.EnumerateArray().FirstOrDefault();
                        if (first2.ValueKind == JsonValueKind.Object)
                        {
                            if (first2.TryGetProperty("message", out var msg2) && msg2.ValueKind == JsonValueKind.Object)
                            {
                                if (msg2.TryGetProperty("content", out var cont2))
                                {
                                    if (cont2.ValueKind == JsonValueKind.String) contentText = cont2.GetString() ?? string.Empty;
                                    else if (cont2.ValueKind == JsonValueKind.Array)
                                    {
                                        var sbb = new System.Text.StringBuilder();
                                        foreach (var seg in cont2.EnumerateArray())
                                        {
                                            if (seg.ValueKind == JsonValueKind.Object && seg.TryGetProperty("text", out var t2))
                                            {
                                                var tx2 = t2.GetString() ?? string.Empty;
                                                if (!string.IsNullOrWhiteSpace(tx2)) sbb.Append(tx2);
                                            }
                                        }
                                        contentText = sbb.ToString();
                                    }
                                }
                            }
                            else if (first2.TryGetProperty("text", out var text2) && text2.ValueKind == JsonValueKind.String)
                            {
                                contentText = text2.GetString() ?? string.Empty;
                            }
                            else if (first2.TryGetProperty("output", out var out2) && out2.ValueKind == JsonValueKind.String)
                            {
                                contentText = out2.GetString() ?? string.Empty;
                            }
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(contentText))
                    {
                        try
                        {
                            using var parsed = JsonDocument.Parse(contentText);
                            if (parsed.RootElement.TryGetProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var act in actions.EnumerateArray())
                                {
                                    if (act.ValueKind != JsonValueKind.Object) continue;
                                    var name = act.TryGetProperty("name", out var nm) ? nm.GetString() ?? string.Empty : string.Empty;
                                    if (string.IsNullOrWhiteSpace(name)) continue;
                                    JsonElement args = default;
                                    if (act.TryGetProperty("args", out var ag)) args = ag.Clone();
                                    var exists = result.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase) &&
                                        ((r.Arguments.ValueKind != JsonValueKind.Undefined ? r.Arguments.GetRawText() : string.Empty) == (args.ValueKind != JsonValueKind.Undefined ? args.GetRawText() : string.Empty)));
                                    if (!exists)
                                    {
                                        result.Add(new ToolCall { Name = name, Arguments = args });
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                    if (result.Count == 0 && !string.IsNullOrWhiteSpace(contentText))
                    {
                        var argsJson = JsonSerializer.Serialize(new { text = contentText });
                        using var aDoc = JsonDocument.Parse(argsJson);
                        result.Add(new ToolCall { Name = "llm_response", Arguments = aDoc.RootElement.Clone() });
                    }
                }
            }
            return result;
        }
    }
}
