using ASG.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using Serilog;

namespace ASG.Api.Data
{
    internal static class DataSeeder
    {
        private class NeteaseApiResponse
        {
            [JsonPropertyName("status")] public bool Status { get; set; }
            [JsonPropertyName("success")] public bool Success { get; set; }
            [JsonPropertyName("code")] public int Code { get; set; }
            [JsonPropertyName("msg")] public string? Msg { get; set; }
            [JsonPropertyName("data")] public List<NeteaseItem>? Data { get; set; }
        }

        private class NeteaseItem
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("camp_id")] public int CampId { get; set; }
            [JsonPropertyName("hero_id")] public int? HeroId { get; set; }
        }

        private class HeroRecord
        {
            public string Name { get; set; } = string.Empty;
            public int CampId { get; set; }
        }

        private class NeteaseStatItem
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("run_rate")] public double? RunRate { get; set; }
            [JsonPropertyName("start_time")] public string? StartTime { get; set; }
            [JsonPropertyName("ping_rate")] public double PingRate { get; set; }
            [JsonPropertyName("ban_rate")] public double BanRate { get; set; }
            [JsonPropertyName("part")] public int? Part { get; set; }
            [JsonPropertyName("use_rate")] public double UseRate { get; set; }
            [JsonPropertyName("end_time")] public string? EndTime { get; set; }
            [JsonPropertyName("season")] public string? Season { get; set; }
            [JsonPropertyName("camp_id")] public int CampId { get; set; }
            [JsonPropertyName("hero_id")] public string? HeroId { get; set; }
            [JsonPropertyName("win_rate")] public double WinRate { get; set; }
            [JsonPropertyName("position")] public string? Position { get; set; }
            [JsonPropertyName("week_num")] public string? WeekNum { get; set; }
        }

        private class NeteaseApiResponseStat
        {
            [JsonPropertyName("status")] public bool Status { get; set; }
            [JsonPropertyName("success")] public bool Success { get; set; }
            [JsonPropertyName("code")] public int Code { get; set; }
            [JsonPropertyName("msg")] public string? Msg { get; set; }
            [JsonPropertyName("data")] public List<NeteaseStatItem>? Data { get; set; }
        }

        public static async Task SeedNeteaseHeroesFromLocalAsync(ApplicationDbContext context, bool overwrite = false)
        {
            var baseDir = AppContext.BaseDirectory;
            var dir = Path.Combine(baseDir, "NeteaseJ");
            if (!Directory.Exists(dir))
            {
                Log.Information("NeteaseJ directory not found: {Dir}", dir);
                return;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            var files = Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);
            Log.Information("Found {FileCount} NeteaseJ files in {Dir}", files.Length, dir);

            if (overwrite)
            {
                try
                {
                    var before = await context.NeteaseHeroes.CountAsync();
                    context.NeteaseHeroes.RemoveRange(context.NeteaseHeroes);
                    await context.SaveChangesAsync();
                    Log.Information("Overwrite enabled: cleared NeteaseHeroes table (removed {Before} rows)", before);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to clear NeteaseHeroes before import");
                }
            }

            var map = new Dictionary<int, HeroRecord>();
            foreach (var file in files)
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    var payload = await JsonSerializer.DeserializeAsync<NeteaseApiResponse>(stream, options);
                    if (payload?.Data == null) continue;
                    foreach (var item in payload.Data)
                    {
                        var heroId = item.HeroId ?? 0;
                        if (heroId == 0) continue;
                        var name = item.Name?.Trim() ?? string.Empty;
                        var campId = item.CampId;
                        if (map.TryGetValue(heroId, out var existing))
                        {
                            if (!string.IsNullOrEmpty(name)) existing.Name = name;
                            existing.CampId = campId;
                        }
                        else
                        {
                            map[heroId] = new HeroRecord { Name = name, CampId = campId };
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to read NeteaseJ file: {File}", file);
                }
            }

            var added = 0;
            var updated = 0;
            foreach (var kv in map)
            {
                var hero = await context.NeteaseHeroes.FindAsync(kv.Key);
                if (hero == null)
                {
                    hero = new NeteaseHero
                    {
                        HeroId = kv.Key,
                        Name = kv.Value.Name,
                        CampId = kv.Value.CampId,
                        UpdatedAt = DateTime.UtcNow
                    };
                    context.NeteaseHeroes.Add(hero);
                    added++;
                }
                else
                {
                    var changed = false;
                    if (!string.Equals(hero.Name, kv.Value.Name, StringComparison.Ordinal)) { hero.Name = kv.Value.Name; changed = true; }
                    if (hero.CampId != kv.Value.CampId) { hero.CampId = kv.Value.CampId; changed = true; }
                    if (changed) { hero.UpdatedAt = DateTime.UtcNow; updated++; }
                }
            }

            if (added + updated > 0)
            {
                await context.SaveChangesAsync();
            }
            var total = await context.NeteaseHeroes.CountAsync();
            Log.Information("Netease heroes seeding completed: {Added} added, {Updated} updated; total={Total}", added, updated, total);
        }

        private static int ParseInt(string? s)
        {
            if (int.TryParse(s, out var v)) return v;
            return 0;
        }

        private static int? ParseNullableInt(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (int.TryParse(s, out var v)) return v;
            return null;
        }

        private static DateTime ParseDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
            if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return EnsureUtc(dt);
            if (DateTime.TryParseExact(s, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return EnsureUtc(dt);
            if (DateTime.TryParse(s, out dt)) return EnsureUtc(dt);
            return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        }

        private static DateTime EnsureUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc) return dt;
            if (dt.Kind == DateTimeKind.Local) return dt.ToUniversalTime();
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        public static async Task SeedNeteaseStatsFromLocalAsync(ApplicationDbContext context, bool overwrite = false)
        {
            var baseDir = AppContext.BaseDirectory;
            var dir = Path.Combine(baseDir, "NeteaseJ");
            if (!Directory.Exists(dir))
            {
                Log.Information("NeteaseJ directory not found: {Dir}", dir);
                return;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            var files = Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);
            Log.Information("Found {FileCount} NeteaseJ files for stats in {Dir}", files.Length, dir);

            if (overwrite)
            {
                try
                {
                    var before = await context.NeteaseHeroStats.CountAsync();
                    context.NeteaseHeroStats.RemoveRange(context.NeteaseHeroStats);
                    await context.SaveChangesAsync();
                    Log.Information("Overwrite enabled: cleared NeteaseHeroStats table (removed {Before} rows)", before);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to clear NeteaseHeroStats before import");
                }
            }

            var added = 0;
            foreach (var file in files)
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    var payload = await JsonSerializer.DeserializeAsync<NeteaseApiResponseStat>(stream, options);
                    if (payload?.Data == null) continue;
                    foreach (var item in payload.Data)
                    {
                        var heroId = ParseInt(item.HeroId);
                        var season = ParseInt(item.Season);
                        var part = item.Part ?? 0;
                        var weekNum = ParseNullableInt(item.WeekNum);
                        var campId = item.CampId;
                        var name = item.Name?.Trim() ?? string.Empty;
                        var start = ParseDate(item.StartTime);
                        var end = ParseDate(item.EndTime);
                        var position = item.Position?.Trim() ?? string.Empty;

                        var hero = await context.NeteaseHeroes.FindAsync(heroId);
                        if (hero == null)
                        {
                            hero = new NeteaseHero { HeroId = heroId, Name = name, CampId = campId, UpdatedAt = DateTime.UtcNow };
                            context.NeteaseHeroes.Add(hero);
                        }
                        else
                        {
                            var changed = false;
                            if (!string.Equals(hero.Name, name, StringComparison.Ordinal)) { hero.Name = name; changed = true; }
                            if (hero.CampId != campId) { hero.CampId = campId; changed = true; }
                            if (changed) hero.UpdatedAt = DateTime.UtcNow;
                        }

                        var exists = await context.NeteaseHeroStats.AnyAsync(e =>
                            e.Source == "netease_local" &&
                            e.HeroId == heroId &&
                            e.CampId == campId &&
                            e.Season == season &&
                            e.Part == part &&
                            e.WeekNum == weekNum &&
                            e.StartDate == start &&
                            e.EndDate == end &&
                            e.Position == position);

                        if (!exists)
                        {
                            var stat = new NeteaseHeroStat
                            {
                                HeroId = heroId,
                                CampId = campId,
                                Season = season,
                                Part = part,
                                WeekNum = weekNum,
                                WinRate = item.WinRate,
                                PingRate = item.PingRate,
                                UseRate = item.UseRate,
                                BanRate = item.BanRate,
                                RunRate = item.RunRate,
                                StartDate = start,
                                EndDate = end,
                                Position = position,
                                Source = "netease_local",
                                FetchedAt = DateTime.UtcNow
                            };
                            context.NeteaseHeroStats.Add(stat);
                            added++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to read NeteaseJ stat file: {File}", file);
                }
            }

            if (added > 0)
            {
                await context.SaveChangesAsync();
            }
            var total = await context.NeteaseHeroStats.CountAsync();
            Log.Information("Netease stats seeding completed: {Added} added; total={Total}", added, total);
        }

        public static async Task SeedDevDataAsync(ApplicationDbContext context, UserManager<User> userManager)
        {

            // 已有数据则跳过（避免重复灌入）
            if (context.Events.Any() && context.Teams.Any())
            {
                return;
            }

            var now = DateTime.UtcNow;

            // 1) 创建 12 个赛事
        }
    }
}
