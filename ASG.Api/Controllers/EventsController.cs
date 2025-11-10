using ASG.Api.DTOs;
using ASG.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace ASG.Api.Controllers
{
    /// <summary>
    /// 赛事管理控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly IEventService _eventService;
        private readonly IWebHostEnvironment _env;

        public EventsController(IEventService eventService, IWebHostEnvironment env)
        {
            _eventService = eventService;
            _env = env;
        }

        /// <summary>
        /// 获取所有赛事
        /// </summary>
        /// <returns>赛事列表</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EventDto>>> GetAllEvents()
        {
            try
            {
                var events = await _eventService.GetAllEventsAsync();
                foreach (var e in events)
                {
                    e.LogoUrl = GetEventLogoUrl(e.Id);
                }
                return Ok(events);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取赛事列表失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 根据ID获取赛事
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <returns>赛事信息</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<EventDto>> GetEvent(Guid id)
        {
            try
            {
                var eventDto = await _eventService.GetEventByIdAsync(id);
                if (eventDto == null)
                {
                    return NotFound(new { message = "赛事不存在" });
                }
                eventDto.LogoUrl = GetEventLogoUrl(id);
                return Ok(eventDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取赛事信息失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 创建赛事（需要登录）
        /// </summary>
        /// <param name="createEventDto">创建赛事DTO</param>
        /// <returns>创建的赛事信息</returns>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<EventDto>> CreateEvent([FromBody] CreateEventDto createEventDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var eventDto = await _eventService.CreateEventAsync(createEventDto, userId);
                eventDto.LogoUrl = GetEventLogoUrl(eventDto.Id);
                return CreatedAtAction(nameof(GetEvent), new { id = eventDto.Id }, eventDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "创建赛事失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 更新赛事（需要登录且为赛事创建者或管理员）
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <param name="updateEventDto">更新赛事DTO</param>
        /// <returns>更新的赛事信息</returns>
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<EventDto>> UpdateEvent(Guid id, [FromBody] UpdateEventDto updateEventDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var eventDto = await _eventService.UpdateEventAsync(id, updateEventDto, userId);
                if (eventDto == null)
                {
                    return NotFound(new { message = "赛事不存在" });
                }

                eventDto.LogoUrl = GetEventLogoUrl(id);
                return Ok(eventDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "更新赛事失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 设置赛事冠军战队（需要登录且为赛事创建者或管理员）。传null清除冠军。
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <param name="dto">设置冠军DTO</param>
        /// <returns>更新后的赛事信息</returns>
        [HttpPut("{id}/champion")]
        [Authorize]
        public async Task<ActionResult<EventDto>> SetChampion(Guid id, [FromBody] SetChampionDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var eventDto = await _eventService.SetChampionTeamAsync(id, dto, userId);
                if (eventDto == null)
                {
                    return NotFound(new { message = "赛事不存在" });
                }

                eventDto.LogoUrl = GetEventLogoUrl(id);
                return Ok(eventDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "设置赛事冠军失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 删除赛事（需要登录且为赛事创建者或管理员）
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> DeleteEvent(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var result = await _eventService.DeleteEventAsync(id, userId);
                if (!result)
                {
                    return NotFound(new { message = "赛事不存在" });
                }

                return Ok(new { message = "赛事删除成功" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "删除赛事失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 战队报名赛事（需要登录）
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <param name="registerDto">报名DTO</param>
        /// <returns>报名信息</returns>
        [HttpPost("{id}/register")]
        [Authorize]
        public async Task<ActionResult<TeamEventDto>> RegisterTeamToEvent(Guid id, [FromBody] RegisterTeamToEventDto registerDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var teamEventDto = await _eventService.RegisterTeamToEventAsync(id, registerDto, userId);
                return Ok(teamEventDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new { message = "报名数据保存失败", error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "战队报名失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 取消战队报名（需要登录）
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <param name="teamId">战队ID</param>
        /// <returns>取消结果</returns>
        [HttpDelete("{id}/register/{teamId}")]
        [Authorize]
        public async Task<ActionResult> UnregisterTeamFromEvent(Guid id, Guid teamId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var result = await _eventService.UnregisterTeamFromEventAsync(id, teamId, userId);
                if (!result)
                {
                    return NotFound(new { message = "报名记录不存在" });
                }

                return Ok(new { message = "取消报名成功" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "取消报名失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 更新战队报名状态（需要登录且为赛事创建者或管理员）
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <param name="teamId">战队ID</param>
        /// <param name="updateDto">更新DTO</param>
        /// <returns>更新的报名信息</returns>
        [HttpPut("{id}/register/{teamId}")]
        [Authorize]
        public async Task<ActionResult<TeamEventDto>> UpdateTeamRegistrationStatus(Guid id, Guid teamId, [FromBody] UpdateTeamRegistrationDto updateDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var teamEventDto = await _eventService.UpdateTeamRegistrationStatusAsync(id, teamId, updateDto, userId);
                if (teamEventDto == null)
                {
                    return NotFound(new { message = "报名记录不存在" });
                }

                return Ok(teamEventDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "更新报名状态失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 获取赛事的报名战队
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <returns>报名战队列表</returns>
        [HttpGet("{id}/registrations")]
        public async Task<ActionResult<IEnumerable<TeamEventDto>>> GetEventRegistrations(Guid id)
        {
            try
            {
                var registrations = await _eventService.GetEventRegistrationsAsync(id);
                return Ok(registrations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取报名列表失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 导出赛事报名信息为CSV（需要登录且为赛事创建者或管理员）
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <returns>CSV文件</returns>
        [HttpGet("{id}/registrations/export")]
        [Authorize]
        public async Task<IActionResult> ExportEventRegistrationsCsv(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var csvBytes = await _eventService.ExportEventRegistrationsCsvAsync(id, userId);
                var eventInfo = await _eventService.GetEventByIdAsync(id);
                var eventName = eventInfo?.Name ?? "event";
                var safeName = Regex.Replace(eventName, "[^a-zA-Z0-9_\\-]", "_");
                var fileName = $"{safeName}-registrations.csv";

                return File(csvBytes, "text/csv; charset=utf-8", fileName);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "导出报名信息失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 获取战队的报名赛事
        /// </summary>
        /// <param name="teamId">战队ID</param>
        /// <returns>报名赛事列表</returns>
        [HttpGet("team/{teamId}/registrations")]
        public async Task<ActionResult<IEnumerable<TeamEventDto>>> GetTeamRegistrations(Guid teamId)
        {
            try
            {
                var registrations = await _eventService.GetTeamRegistrationsAsync(teamId);
                return Ok(registrations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取战队报名列表失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 获取用户创建的赛事（需要登录）
        /// </summary>
        /// <returns>用户创建的赛事列表</returns>
        [HttpGet("my-events")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<EventDto>>> GetMyEvents()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var events = await _eventService.GetEventsByCreatorAsync(userId);
                foreach (var e in events)
                {
                    e.LogoUrl = GetEventLogoUrl(e.Id);
                }
                return Ok(events);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取我的赛事失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 获取正在报名的赛事
        /// </summary>
        /// <returns>正在报名的赛事列表</returns>
        [HttpGet("active-registration")]
        public async Task<ActionResult<IEnumerable<EventDto>>> GetActiveRegistrationEvents()
        {
            try
            {
                var events = await _eventService.GetActiveRegistrationEventsAsync();
                foreach (var e in events)
                {
                    e.LogoUrl = GetEventLogoUrl(e.Id);
                }
                return Ok(events);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取正在报名的赛事失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 获取正在报名的赛事（分页）
        /// </summary>
        /// <param name="page">页码（默认1）</param>
        /// <param name="pageSize">每页大小（默认12）</param>
        /// <returns>分页结果</returns>
        [HttpGet("active-registration/paged")]
        public async Task<ActionResult<PagedResult<EventDto>>> GetActiveRegistrationEventsPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
        {
            try
            {
                var result = await _eventService.GetActiveRegistrationEventsAsync(page, pageSize);
                foreach (var e in result.Items)
                {
                    e.LogoUrl = GetEventLogoUrl(e.Id);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取正在报名的赛事（分页）失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 获取即将开始的赛事
        /// </summary>
        /// <returns>即将开始的赛事列表</returns>
        [HttpGet("upcoming")]
        public async Task<ActionResult<IEnumerable<EventDto>>> GetUpcomingEvents()
        {
            try
            {
                var events = await _eventService.GetUpcomingEventsAsync();
                foreach (var e in events)
                {
                    e.LogoUrl = GetEventLogoUrl(e.Id);
                }
                return Ok(events);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取即将开始的赛事失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 获取即将开始的赛事（分页）
        /// </summary>
        /// <param name="page">页码（默认1）</param>
        /// <param name="pageSize">每页大小（默认12）</param>
        /// <returns>分页结果</returns>
        [HttpGet("upcoming/paged")]
        public async Task<ActionResult<PagedResult<EventDto>>> GetUpcomingEventsPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
        {
            try
            {
                var result = await _eventService.GetUpcomingEventsAsync(page, pageSize);
                foreach (var e in result.Items)
                {
                    e.LogoUrl = GetEventLogoUrl(e.Id);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取即将开始的赛事（分页）失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 上传赛事徽标（需要登录且为赛事创建者或管理员）
        /// </summary>
        [HttpPost("{id}/logo")]
        [Authorize]
        public async Task<IActionResult> UploadEventLogo(Guid id, [FromForm] IFormFile? logo)
        {
            try
            {
                if (logo == null || logo.Length == 0)
                {
                    return BadRequest(new { message = "请选择要上传的徽标文件" });
                }

                var allowedExts = new[] { ".png", ".jpg", ".jpeg", ".webp" };
                var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
                if (!allowedExts.Contains(ext))
                {
                    return BadRequest(new { message = "仅支持 png/jpg/jpeg/webp 格式" });
                }

                const long maxSize = 5 * 1024 * 1024; // 5MB
                if (logo.Length > maxSize)
                {
                    return BadRequest(new { message = "文件大小不能超过 5MB" });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var canManage = await _eventService.CanUserManageEventAsync(id, userId);
                if (!canManage)
                {
                    return Forbid("您没有权限上传该赛事的徽标");
                }

                var root = _env.WebRootPath;
                if (string.IsNullOrEmpty(root))
                {
                    root = Path.Combine(_env.ContentRootPath, "wwwroot");
                }

                var eventDir = Path.Combine(root, "event-logos", id.ToString());
                Directory.CreateDirectory(eventDir);

                foreach (var file in Directory.GetFiles(eventDir, "logo.*"))
                {
                    try { System.IO.File.Delete(file); } catch { /* ignore */ }
                }

                var filePath = Path.Combine(eventDir, $"logo{ext}");
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await logo.CopyToAsync(stream);
                }

                var url = GetEventLogoUrl(id);
                return Ok(new { logoUrl = url });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "上传徽标失败", error = ex.Message });
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
    }
}