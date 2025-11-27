using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ASG.Api.Data;
using ASG.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ASG.Api.Services
{
    public class NeteaseHeroStatsIngestService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NeteaseHeroStatsIngestService> _logger;

        public NeteaseHeroStatsIngestService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<NeteaseHeroStatsIngestService> logger)
        {
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var runOnStartup = _configuration.GetValue<bool?>("HeroStatsIngest:RunOnStartup") ?? true;
            var intervalHours = _configuration.GetValue<int?>("HeroStatsIngest:IntervalHours") ?? 96;

            if (runOnStartup)
            {
                try { await FetchAndSaveAsync(stoppingToken); } catch (Exception ex) { _logger.LogError(ex, "Hero stats ingest on startup failed"); }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken); }
                catch (TaskCanceledException) { break; }

                try { await FetchAndSaveAsync(stoppingToken); }
                catch (Exception ex) { _logger.LogError(ex, "Hero stats ingest failed"); }
            }
        }

        private async Task FetchAndSaveAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Schema is managed by EF Core migrations

            var client = _httpClientFactory.CreateClient("netease");
            var response = await client.GetAsync("/w/epro/wechatuser/gameRole/herouse_week", ct);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(ct);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
            var payload = await JsonSerializer.DeserializeAsync<ApiResponse>(stream, options, ct);
            if (payload == null || payload.Data == null) return;

            foreach (var item in payload.Data)
            {
                var heroId = item.HeroId ?? 0;
                var season = item.Season ?? 0;
                var part = item.Part ?? 0;
                var weekNum = item.WeekNum;
                var campId = item.CampId;
                var name = item.Name?.Trim() ?? string.Empty;
                var start = ParseDate(item.StartTime);
                var end = ParseDate(item.EndTime);
                var position = item.Position?.Trim() ?? string.Empty;

                var hero = await context.NeteaseHeroes.FindAsync(new object[] { heroId }, ct);
                if (hero == null)
                {
                    hero = new NeteaseHero
                    {
                        HeroId = heroId,
                        Name = name,
                        CampId = campId,
                        UpdatedAt = DateTime.UtcNow
                    };
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
                    e.Source == "netease" &&
                    e.HeroId == heroId &&
                    e.CampId == campId &&
                    e.Season == season &&
                    e.Part == part &&
                    e.WeekNum == weekNum &&
                    e.StartDate == start &&
                    e.EndDate == end &&
                    e.Position == position, ct);

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
                        Source = "netease",
                        FetchedAt = DateTime.UtcNow
                    };
                    context.NeteaseHeroStats.Add(stat);
                }
            }

            await context.SaveChangesAsync(ct);
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

        private class ApiResponse
        {
            [JsonPropertyName("status")] public bool Status { get; set; }
            [JsonPropertyName("success")] public bool Success { get; set; }
            [JsonPropertyName("code")] public int Code { get; set; }
            [JsonPropertyName("msg")] public string? Msg { get; set; }
            [JsonPropertyName("data")] public List<Item>? Data { get; set; }
        }

        private class Item
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("camp_id")] public int CampId { get; set; }
            [JsonPropertyName("hero_id")] public int? HeroId { get; set; }
            [JsonPropertyName("season")] public int? Season { get; set; }
            [JsonPropertyName("part")] public int? Part { get; set; }
            [JsonPropertyName("week_num")] public int? WeekNum { get; set; }
            [JsonPropertyName("win_rate")] public double WinRate { get; set; }
            [JsonPropertyName("ping_rate")] public double PingRate { get; set; }
            [JsonPropertyName("use_rate")] public double UseRate { get; set; }
            [JsonPropertyName("ban_rate")] public double BanRate { get; set; }
            [JsonPropertyName("run_rate")] public double? RunRate { get; set; }
            [JsonPropertyName("start_time")] public string? StartTime { get; set; }
            [JsonPropertyName("end_time")] public string? EndTime { get; set; }
            [JsonPropertyName("position")] public string? Position { get; set; }
        }
    }
}
