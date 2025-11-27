using ASG.Api.DTOs;
using ASG.Api.Repositories;
using System.Text.Json;

namespace ASG.Api.Services
{
    public class PayrollService : IPayrollService
    {
        private readonly IMatchRepository _matchRepo;
        private readonly IEventRepository _eventRepo;
        public PayrollService(IMatchRepository matchRepo, IEventRepository eventRepo) { _matchRepo = matchRepo; _eventRepo = eventRepo; }

        public async Task<IEnumerable<PayrollEntryDto>> GetPayrollAsync(string userId, DateTime? from, DateTime? to)
        {
            var events = await _eventRepo.GetAllEventsAsync();
            var result = new List<PayrollEntryDto>();
            foreach (var ev in events)
            {
                var matches = await _matchRepo.GetAllMatchesAsync(ev.Id, 1, int.MaxValue);
                foreach (var m in matches)
                {
                    var dt = m.MatchTime;
                    if (from.HasValue && dt < from.Value) continue;
                    if (to.HasValue && dt > to.Value) continue;
                    var custom = ParseCustom(m.CustomData);
                    foreach (var role in new[] { "commentatorUserId", "directorUserId", "refereeUserId" })
                    {
                        if (custom.TryGetValue(role, out var uidObj) && (uidObj?.ToString() ?? string.Empty) == userId)
                        {
                            var pay = custom.TryGetValue("payPerMatch", out var p) ? SafeInt(p) : 0;
                            var pos = role.Contains("commentator") ? "Commentator" : role.Contains("director") ? "Director" : "Referee";
                            result.Add(new PayrollEntryDto
                            {
                                Id = m.Id,
                                Date = dt.ToString("yyyy-MM-dd"),
                                EventName = ev.Name,
                                MatchTitle = $"{m.HomeTeam?.Name ?? ""} VS {m.AwayTeam?.Name ?? ""}",
                                PositionType = pos,
                                PayPerMatch = pay,
                                Amount = pay,
                            });
                        }
                    }
                }
            }
            return result.OrderBy(r => r.Date);
        }

        private static Dictionary<string, object> ParseCustom(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new Dictionary<string, object>();
            try { return JsonSerializer.Deserialize<Dictionary<string, object>>(s!) ?? new Dictionary<string, object>(); } catch { return new Dictionary<string, object>(); }
        }

        private static int SafeInt(object? v)
        {
            if (v == null) return 0;
            if (v is int i) return i;
            if (v is long l) return (int)l;
            if (v is double d) return (int)d;
            if (v is float f) return (int)f;
            if (v is decimal dec) return (int)dec;
            if (v is string s)
            {
                if (int.TryParse(s, out var si)) return si;
                if (double.TryParse(s, out var sd)) return (int)sd;
                return 0;
            }
            if (v is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Number)
                {
                    if (je.TryGetInt32(out var ji)) return ji;
                    if (je.TryGetInt64(out var jl)) return (int)jl;
                    if (je.TryGetDecimal(out var jd)) return (int)jd;
                }
                else if (je.ValueKind == JsonValueKind.String)
                {
                    var str = je.GetString();
                    if (int.TryParse(str, out var si)) return si;
                    if (double.TryParse(str, out var sd)) return (int)sd;
                }
            }
            try { return Convert.ToInt32(v); } catch { return 0; }
        }
    }
}
