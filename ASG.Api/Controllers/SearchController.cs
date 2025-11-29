using ASG.Api.DTOs;
using ASG.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ITeamService _teamService;
        private readonly IEventService _eventService;
        private readonly IArticleService _articleService;
        private readonly IWebHostEnvironment _env;

        public SearchController(ITeamService teamService, IEventService eventService, IArticleService articleService, IWebHostEnvironment env)
        {
            _teamService = teamService;
            _eventService = eventService;
            _articleService = articleService;
            _env = env;
        }

        /// <summary>
        /// 综合搜索：type=teams/events/articles/all，支持分页
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<object>> Search([FromQuery] string type = "all", [FromQuery] string query = "", [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
        {
            type = (type ?? "all").ToLowerInvariant();
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 12;

            switch (type)
            {
                case "teams":
                {
                    var teams = await _teamService.SearchTeamsByNameAsync(query, page, pageSize);
                    foreach (var t in teams.Items)
                    {
                        t.LogoUrl = GetTeamLogoUrl(t.Id);
                    }
                    return Ok(teams);
                }
                case "events":
                {
                    var events = await _eventService.SearchEventsAsync(query, page, pageSize);
                    foreach (var e in events.Items)
                    {
                        e.LogoUrl = GetEventLogoUrl(e.Id);
                    }
                    return Ok(events);
                }
                case "articles":
                {
                    var articles = await _articleService.SearchArticlesAsync(query, page, pageSize);
                    return Ok(articles);
                }
                case "all":
                default:
                {
                    PagedResult<TeamDto> teams;
                    PagedResult<EventDto> events;
                    PagedResult<ArticleDto> articles;

                    try
                    {
                        teams = await _teamService.SearchTeamsByNameAsync(query, page, pageSize);
                    }
                    catch
                    {
                        teams = new PagedResult<TeamDto> { Items = Array.Empty<TeamDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
                    }

                    try
                    {
                        events = await _eventService.SearchEventsAsync(query, page, pageSize);
                    }
                    catch
                    {
                        events = new PagedResult<EventDto> { Items = Array.Empty<EventDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
                    }

                    try
                    {
                        articles = await _articleService.SearchArticlesAsync(query, page, pageSize);
                    }
                    catch
                    {
                        articles = new PagedResult<ArticleDto> { Items = Array.Empty<ArticleDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
                    }

                    foreach (var t in teams.Items)
                    {
                        t.LogoUrl = GetTeamLogoUrl(t.Id);
                    }
                    foreach (var e in events.Items)
                    {
                        e.LogoUrl = GetEventLogoUrl(e.Id);
                    }

                    var aggregate = new SearchAggregateDto
                    {
                        Teams = teams,
                        Events = events,
                        Articles = articles
                    };
                    return Ok(aggregate);
                }
            }
        }

        private string? GetEventLogoUrl(Guid eventId)
        {
            var root = _env.WebRootPath;
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(_env.ContentRootPath, "wwwroot");
            }
            var eventDir = Path.Combine(root, "event-logos", eventId.ToString());
            if (!Directory.Exists(eventDir)) return null;
            var files = Directory.GetFiles(eventDir, "logo.*");
            if (files.Length == 0) return null;
            var fileName = Path.GetFileName(files[0]);
            var relativePath = $"/event-logos/{eventId}/{fileName}";
            var scheme = Request.Scheme;
            var host = Request.Host.HasValue ? Request.Host.Value : string.Empty;
            if (!string.IsNullOrEmpty(host))
            {
                return $"{scheme}://{host}{relativePath}";
            }
            return relativePath;
        }

        private string? GetTeamLogoUrl(Guid teamId)
        {
            var root = _env.WebRootPath;
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(_env.ContentRootPath, "wwwroot");
            }
            var teamDir = Path.Combine(root, "team-logos", teamId.ToString());
            if (!Directory.Exists(teamDir)) return null;
            var files = Directory.GetFiles(teamDir, "logo.*");
            if (files.Length == 0) return null;
            var fileName = Path.GetFileName(files[0]);
            var relativePath = $"/team-logos/{teamId}/{fileName}";
            var scheme = Request.Scheme;
            var host = Request.Host.HasValue ? Request.Host.Value : string.Empty;
            if (!string.IsNullOrEmpty(host))
            {
                return $"{scheme}://{host}{relativePath}";
            }
            return relativePath;
        }
    }
}
