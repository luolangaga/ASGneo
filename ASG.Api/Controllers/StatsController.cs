using ASG.Api.Data;
using ASG.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        public StatsController(ApplicationDbContext db, IMemoryCache cache) { _db = db; _cache = cache; }

        private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        private const double MinUseWeight = 0.005;
        private static double SafeWeightedAvg(IEnumerable<(double value, double weight)> items)
        {
            double wsum = 0, vsum = 0; int count = 0;
            foreach (var (value, weight) in items)
            {
                var w = double.IsNaN(weight) ? 0 : Math.Max(weight, 0);
                var v = double.IsNaN(value) ? 0 : value;
                wsum += w; vsum += v * w; count++;
            }
            if (wsum <= 0) return count == 0 ? 0 : items.Average(i => i.value);
            return vsum / wsum;
        }
        private static double SafeWeightedAvgFiltered(IEnumerable<(double value, double weight)> items, double minWeight)
        {
            var filtered = items.Where(t => !double.IsNaN(t.weight) && t.weight >= minWeight).ToList();
            if (filtered.Count == 0) return SafeWeightedAvg(items);
            return SafeWeightedAvg(filtered);
        }
        private static double SafeWeightedAvgSmoothedFiltered(IEnumerable<(double value, double weight)> items, double minWeight, double globalMean, double alpha)
        {
            var filtered = items.Where(t => !double.IsNaN(t.weight) && t.weight >= minWeight).ToList();
            if (filtered.Count == 0)
            {
                var avg = SafeWeightedAvg(items);
                double wsum0 = items.Sum(t => double.IsNaN(t.weight) ? 0 : Math.Max(t.weight, 0));
                return (avg * wsum0 + alpha * globalMean) / Math.Max(wsum0 + alpha, 1e-9);
            }
            double wsum = filtered.Sum(t => Math.Max(t.weight, 0));
            double vsum = filtered.Sum(t => Math.Max(t.weight, 0) * t.value);
            return (vsum + alpha * globalMean) / Math.Max(wsum + alpha, 1e-9);
        }
        private static double? SafeWeightedAvgNullable(IEnumerable<(double? value, double? weight)> items)
        {
            double wsum = 0, vsum = 0; int count = 0;
            foreach (var (v0, w0) in items)
            {
                if (v0 == null || w0 == null) continue;
                var w = double.IsNaN(w0.Value) ? 0 : Math.Max(w0.Value, 0);
                var v = double.IsNaN(v0.Value) ? 0 : v0.Value;
                wsum += w; vsum += v * w; count++;
            }
            if (count == 0) return null;
            if (wsum <= 0) return items.Where(x => x.value != null).Select(x => x.value!.Value).DefaultIfEmpty(0).Average();
            var res = vsum / wsum;
            return res;
        }

        [HttpGet("heroes")]
        public async Task<ActionResult<IEnumerable<HeroBriefDto>>> GetHeroes([FromQuery] int? campId = null)
        {
            try
            {
                var key = $"heroes:{(campId.HasValue ? campId.Value.ToString() : "all")}";
                var list = await _cache.GetOrCreateAsync(key, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                    var q = _db.NeteaseHeroes.AsNoTracking();
                    if (campId.HasValue) q = q.Where(x => x.CampId == campId.Value);
                    return await q.OrderBy(x => x.CampId).ThenBy(x => x.HeroId)
                        .Select(x => new HeroBriefDto { HeroId = x.HeroId, Name = x.Name, CampId = x.CampId })
                        .ToListAsync();
                });
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取角色列表失败", error = ex.Message });
            }
        }

        [HttpGet("meta")]
        public async Task<ActionResult<StatsMetaDto>> GetMeta()
        {
            try
            {
                var dto = await _cache.GetOrCreateAsync("stats:meta", async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                    var seasons = await _db.NeteaseHeroStats.AsNoTracking().Select(x => x.Season).Distinct().OrderBy(x => x).ToListAsync();
                    var parts = await _db.NeteaseHeroStats.AsNoTracking().Select(x => x.Part).Distinct().OrderBy(x => x).ToListAsync();
                    var camps = await _db.NeteaseHeroStats.AsNoTracking().Select(x => x.CampId).Distinct().OrderBy(x => x).ToListAsync();
                    var positionsRaw = await _db.NeteaseHeroStats.AsNoTracking().Select(x => x.Position).Distinct().ToListAsync();
                    var positions = positionsRaw.SelectMany(p => (p ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        .Select(s => s.Trim()).Where(s => s.Length > 0).Distinct().OrderBy(s => s).ToList();
                    return new StatsMetaDto { Seasons = seasons, Parts = parts, Camps = camps, Positions = positions };
                });
                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取元数据失败", error = ex.Message });
            }
        }

        [HttpGet("overview")]
        public async Task<ActionResult<IEnumerable<OverviewItemDto>>> GetOverview([FromQuery] int? season = null, [FromQuery] int? part = null, [FromQuery] int? campId = null, [FromQuery] string? position = null, [FromQuery] int? top = null, [FromQuery] string metric = "use_rate", [FromQuery] string? avgMode = null)
        {
            try
            {
                var key = $"overview:{season}:{part}:{campId}:{position}:{top}:{metric}";
                var items = await _cache.GetOrCreateAsync(key, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                    var q = _db.NeteaseHeroStats.AsNoTracking();
                    if (season.HasValue) q = q.Where(x => x.Season == season.Value);
                    if (part.HasValue) q = q.Where(x => x.Part == part.Value);
                    if (campId.HasValue) q = q.Where(x => x.CampId == campId.Value);
                    if (!string.IsNullOrWhiteSpace(position)) q = q.Where(x => (x.Position ?? "").Contains(position));
                    q = q.Where(x => x.WinRate > 0 && x.WinRate < 1);

                    var allList = await q.ToListAsync();
                    double globalRegWinMean = SafeWeightedAvg(allList.Where(x => x.CampId == 1).Select(x => (x.WinRate, x.UseRate)));
                    double globalSurWinMean = SafeWeightedAvg(allList.Where(x => x.CampId == 2).Select(x => (x.WinRate, x.UseRate)));
                    var mode = (avgMode ?? "equal_date").ToLowerInvariant();
                    var groupList = allList.GroupBy(x => new { x.HeroId, x.CampId }).ToList();
                    var grouped = groupList.Select(g => {
                        if (mode == "official" || mode == "weighted")
                        {
                            return new {
                                g.Key.HeroId,
                                g.Key.CampId,
                                WinRateAvg = SafeWeightedAvgFiltered(g.Select(s => (s.WinRate, s.UseRate)), MinUseWeight),
                                PingRateAvg = SafeWeightedAvgFiltered(g.Select(s => (s.PingRate, s.UseRate)), MinUseWeight),
                                UseRateAvg = g.Average(s => s.UseRate),
                                BanRateAvg = SafeWeightedAvgFiltered(g.Select(s => (s.BanRate, s.UseRate)), MinUseWeight),
                                RunRateAvg = SafeWeightedAvgNullable(g.Select(s => (s.RunRate, (double?)s.UseRate))),
                                LatestDate = g.Max(x => x.StartDate),
                                LatestSet = g.Where(x => x.StartDate == g.Max(y => y.StartDate)).ToList()
                            };
                        }
                        else
                        {
                            var weekly = g.GroupBy(x => x.StartDate).Select(week => new {
                                Win = week.Average(s => s.WinRate),
                                Ping = week.Average(s => s.PingRate),
                                Use = week.Average(s => s.UseRate),
                                Ban = week.Average(s => s.BanRate),
                                Run = week.Select(s => s.RunRate).Any(r => r != null) ? (double?)week.Select(s => s.RunRate).Where(r => r != null).Select(r => r!.Value).DefaultIfEmpty(0).Average() : (double?)null
                            }).ToList();
                            return new {
                                g.Key.HeroId,
                                g.Key.CampId,
                                WinRateAvg = weekly.Select(w => w.Win).DefaultIfEmpty(0).Average(),
                                PingRateAvg = weekly.Select(w => w.Ping).DefaultIfEmpty(0).Average(),
                                UseRateAvg = weekly.Select(w => w.Use).DefaultIfEmpty(0).Average(),
                                BanRateAvg = weekly.Select(w => w.Ban).DefaultIfEmpty(0).Average(),
                                RunRateAvg = weekly.Select(w => w.Run).Any(r => r != null) ? weekly.Where(w => w.Run != null).Select(w => w.Run!.Value).DefaultIfEmpty(0).Average() : (double?)null,
                                LatestDate = g.Max(x => x.StartDate),
                                LatestSet = g.Where(x => x.StartDate == g.Max(y => y.StartDate)).ToList()
                            };
                        }
                    }).ToList();

                    var metricLower = (metric ?? "use_rate").ToLowerInvariant();
                    var sorted = grouped.OrderByDescending(d =>
                        (d.LatestSet == null || d.LatestSet.Count == 0) ? 0.0 : metricLower switch
                        {
                            "win_rate" => (mode == "official" || mode == "weighted") ? SafeWeightedAvgFiltered(d.LatestSet.Select(x => (x.WinRate, x.UseRate)), MinUseWeight) : d.LatestSet.Average(x => x.WinRate),
                            "ban_rate" => (mode == "official" || mode == "weighted") ? SafeWeightedAvgFiltered(d.LatestSet.Select(x => (x.BanRate, x.UseRate)), MinUseWeight) : d.LatestSet.Average(x => x.BanRate),
                            "ping_rate" => (mode == "official" || mode == "weighted") ? SafeWeightedAvgFiltered(d.LatestSet.Select(x => (x.PingRate, x.UseRate)), MinUseWeight) : d.LatestSet.Average(x => x.PingRate),
                            "use_rate" => d.LatestSet.Average(x => x.UseRate),
                            _ => d.LatestSet.Average(x => x.UseRate)
                        }
                    ).ToList();
                    if (top.HasValue && top.Value > 0) sorted = sorted.Take(top.Value).ToList();
                    var heroIds = sorted.Select(x => (int)x.HeroId).ToList();
                    var nameMap = await _db.NeteaseHeroes.AsNoTracking().Where(h => heroIds.Contains(h.HeroId))
                        .ToDictionaryAsync(h => h.HeroId, h => h.Name);

                    return sorted.Select(x => new OverviewItemDto
                    {
                        HeroId = (int)x.HeroId,
                        Name = nameMap.TryGetValue((int)x.HeroId, out var n) ? n : ("#" + x.HeroId),
                        CampId = (int)x.CampId,
                        WinRateAvg = Clamp01((double)x.WinRateAvg),
                        PingRateAvg = Clamp01((double)x.PingRateAvg),
                        UseRateAvg = Clamp01((double)x.UseRateAvg),
                        BanRateAvg = Clamp01((double)x.BanRateAvg),
                        RunRateAvg = x.RunRateAvg == null ? null : Clamp01((double)x.RunRateAvg),
                        MetricLatest = (x.LatestSet == null || x.LatestSet.Count == 0) ? 0.0 : metricLower switch
                        {
                            "win_rate" => (mode == "official" || mode == "weighted") ? SafeWeightedAvgFiltered(x.LatestSet.Select(s => (s.WinRate, s.UseRate)), MinUseWeight) : x.LatestSet.Average(s => s.WinRate),
                            "ban_rate" => (mode == "official" || mode == "weighted") ? SafeWeightedAvgFiltered(x.LatestSet.Select(s => (s.BanRate, s.UseRate)), MinUseWeight) : x.LatestSet.Average(s => s.BanRate),
                            "ping_rate" => (mode == "official" || mode == "weighted") ? SafeWeightedAvgFiltered(x.LatestSet.Select(s => (s.PingRate, s.UseRate)), MinUseWeight) : x.LatestSet.Average(s => s.PingRate),
                            "use_rate" => x.LatestSet.Average(s => s.UseRate),
                            _ => x.LatestSet.Average(s => s.UseRate)
                        }
                    }).ToList();
                });
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取概览失败", error = ex.Message });
            }
        }

        [HttpGet("trend/{heroId}")]
        public async Task<ActionResult<IEnumerable<StatPointDto>>> GetTrend([FromRoute] int heroId, [FromQuery] int? season = null, [FromQuery] int? part = null, [FromQuery] string? position = null)
        {
            try
            {
                var key = $"trend:{heroId}:{season}:{part}:{position}";
                var list = await _cache.GetOrCreateAsync(key, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                    var q = _db.NeteaseHeroStats.AsNoTracking().Where(x => x.HeroId == heroId);
                    if (season.HasValue) q = q.Where(x => x.Season == season.Value);
                    if (part.HasValue) q = q.Where(x => x.Part == part.Value);
                    if (!string.IsNullOrWhiteSpace(position)) q = q.Where(x => (x.Position ?? "").Contains(position));
                    q = q.Where(x => x.WinRate > 0 && x.WinRate < 1);

                    if (!part.HasValue)
                    {
                        var grouped = await q.GroupBy(x => x.StartDate).ToListAsync();
                        return grouped.OrderBy(g => g.Key).Select(g => new StatPointDto
                        {
                            Date = g.Key,
                            WinRate = Clamp01(SafeWeightedAvgFiltered(g.Select(x => (x.WinRate, x.UseRate)), MinUseWeight)),
                            PingRate = Clamp01(SafeWeightedAvgFiltered(g.Select(x => (x.PingRate, x.UseRate)), MinUseWeight)),
                            UseRate = Clamp01(g.Average(x => x.UseRate)),
                            BanRate = Clamp01(SafeWeightedAvgFiltered(g.Select(x => (x.BanRate, x.UseRate)), MinUseWeight)),
                            RunRate = SafeWeightedAvgNullable(g.Select(x => (x.RunRate, (double?)x.UseRate)))
                        }).ToList();
                    }

                    return await q.OrderBy(x => x.StartDate).Select(x => new StatPointDto
                    {
                        Date = x.StartDate,
                        WinRate = x.WinRate,
                        PingRate = x.PingRate,
                        UseRate = x.UseRate,
                        BanRate = x.BanRate,
                        RunRate = x.RunRate
                    }).ToListAsync();
                });
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取趋势失败", error = ex.Message });
            }
        }

        [HttpGet("compare")]
        public async Task<ActionResult<IEnumerable<CompareSeriesDto>>> Compare([FromQuery] string heroIds, [FromQuery] string metric = "win_rate", [FromQuery] int? season = null, [FromQuery] int? part = null, [FromQuery] string? position = null)
        {
            try
            {
                var ids = (heroIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var v) ? v : 0).Where(v => v > 0).Distinct().ToList();
                if (ids.Count == 0) return BadRequest(new { message = "请提供 heroIds（逗号分隔）" });

                var key = $"compare:{string.Join('-', ids.OrderBy(x => x))}:{metric}:{season}:{part}:{position}";
                var series = await _cache.GetOrCreateAsync(key, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                    var q = _db.NeteaseHeroStats.AsNoTracking().Where(x => ids.Contains(x.HeroId));
                    if (season.HasValue) q = q.Where(x => x.Season == season.Value);
                    if (part.HasValue) q = q.Where(x => x.Part == part.Value);
                    if (!string.IsNullOrWhiteSpace(position)) q = q.Where(x => (x.Position ?? "").Contains(position));
                    q = q.Where(x => x.WinRate > 0 && x.WinRate < 1);

                    var all = await q.ToListAsync();
                    var groups = all.GroupBy(x => x.HeroId).ToList();
                    var nameMap = await _db.NeteaseHeroes.AsNoTracking().Where(h => ids.Contains(h.HeroId)).ToDictionaryAsync(h => h.HeroId, h => h.Name);

                    double pick(Models.NeteaseHeroStat s) => metric.ToLowerInvariant() switch
                    {
                        "win_rate" => s.WinRate,
                        "ban_rate" => s.BanRate,
                        "ping_rate" => s.PingRate,
                        "use_rate" => s.UseRate,
                        _ => s.WinRate
                    };

                    return groups.Select(g => new CompareSeriesDto
                    {
                        HeroId = g.Key,
                        Name = nameMap.TryGetValue(g.Key, out var n) ? n : ("#" + g.Key),
                        Points = g.GroupBy(x => x.StartDate).OrderBy(gg => gg.Key)
                            .Select(gg => new ComparePointDto {
                                Date = gg.Key,
                                Value = Clamp01(SafeWeightedAvgFiltered(gg.Select(s => (pick(s), s.UseRate)), MinUseWeight))
                            }).ToList()
                    }).ToList();
                });

                return Ok(series);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "对比分析失败", error = ex.Message });
            }
        }

        [HttpGet("camp-trend")]
        public async Task<ActionResult<IEnumerable<CompareSeriesDto>>> GetCampTrend([FromQuery] int? season = null, [FromQuery] int? part = null, [FromQuery] string? position = null)
        {
            try
            {
                var key = $"camp-trend:{season}:{part}:{position}";
                var series = await _cache.GetOrCreateAsync(key, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                    var q = _db.NeteaseHeroStats.AsNoTracking();
                    if (season.HasValue) q = q.Where(x => x.Season == season.Value);
                    if (part.HasValue) q = q.Where(x => x.Part == part.Value);
                    if (!string.IsNullOrWhiteSpace(position)) q = q.Where(x => (x.Position ?? "").Contains(position));
                    q = q.Where(x => x.WinRate > 0 && x.WinRate < 1);

                    var regGroups = await q.Where(x => x.CampId == 1).GroupBy(x => x.StartDate).ToListAsync();
                    var surGroups = await q.Where(x => x.CampId == 2).GroupBy(x => x.StartDate).ToListAsync();

                    var regSeries = new CompareSeriesDto
                    {
                        HeroId = 1,
                        Name = "监管者",
                        Points = regGroups.OrderBy(g => g.Key)
                            .Select(g => new ComparePointDto { Date = g.Key, Value = Clamp01(SafeWeightedAvgFiltered(g.Select(x => (x.WinRate, x.UseRate)), MinUseWeight)) }).ToList()
                    };
                    var surSeries = new CompareSeriesDto
                    {
                        HeroId = 2,
                        Name = "求生者",
                        Points = surGroups.OrderBy(g => g.Key)
                            .Select(g => new ComparePointDto { Date = g.Key, Value = Clamp01(SafeWeightedAvgFiltered(g.Select(x => (x.WinRate, x.UseRate)), MinUseWeight)) }).ToList()
                    };

                    return new List<CompareSeriesDto> { regSeries, surSeries };
                });
                return Ok(series);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "阵营胜率对比失败", error = ex.Message });
            }
        }
    }
}
