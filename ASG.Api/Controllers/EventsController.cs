using ASG.Api.DTOs;
using ASG.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

        public EventsController(IEventService eventService)
        {
            _eventService = eventService;
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
        /// 团队报名赛事（需要登录）
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
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "团队报名失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 取消团队报名（需要登录）
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <param name="teamId">团队ID</param>
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
        /// 更新团队报名状态（需要登录且为赛事创建者或管理员）
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <param name="teamId">团队ID</param>
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
        /// 获取赛事的报名团队
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <returns>报名团队列表</returns>
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
        /// 获取团队的报名赛事
        /// </summary>
        /// <param name="teamId">团队ID</param>
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
                return StatusCode(500, new { message = "获取团队报名列表失败", error = ex.Message });
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
                return Ok(events);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取正在报名的赛事失败", error = ex.Message });
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
                return Ok(events);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取即将开始的赛事失败", error = ex.Message });
            }
        }
    }
}