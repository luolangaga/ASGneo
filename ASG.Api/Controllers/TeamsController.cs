using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TeamsController : ControllerBase
    {
        private readonly ITeamService _teamService;
        private readonly ILogger<TeamsController> _logger;

        public TeamsController(ITeamService teamService, ILogger<TeamsController> logger)
        {
            _teamService = teamService;
            _logger = logger;
        }

        /// <summary>
        /// 获取所有团队列表（分页）
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<TeamDto>>> GetAllTeams([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _teamService.GetAllTeamsAsync(page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取团队列表时发生错误");
                return StatusCode(500, "获取团队列表失败");
            }
        }

        /// <summary>
        /// 根据ID获取团队详情
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<TeamDto>> GetTeam(Guid id)
        {
            try
            {
                var team = await _teamService.GetTeamByIdAsync(id);
                if (team == null)
                {
                    return NotFound("团队不存在");
                }
                return Ok(team);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取团队详情时发生错误，团队ID: {TeamId}", id);
                return StatusCode(500, "获取团队详情失败");
            }
        }

        /// <summary>
        /// 创建新团队（需要登录，自动绑定到当前用户）
        /// </summary>
        [HttpPost("register")]
        [Authorize]
        public async Task<ActionResult<TeamDto>> RegisterTeam([FromBody] CreateTeamDto createTeamDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // 获取当前登录用户ID
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("无法获取用户信息");
                }

                var team = await _teamService.CreateTeamAsync(createTeamDto, userId);
                return CreatedAtAction(nameof(GetTeam), new { id = team.Id }, team);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建团队时发生错误");
                return StatusCode(500, "创建团队失败");
            }
        }

        /// <summary>
        /// 更新团队信息（需要登录且为团队拥有者）
        /// </summary>
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<TeamDto>> UpdateTeam(Guid id, [FromBody] UpdateTeamDto updateTeamDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("用户未登录");
                }

                var team = await _teamService.UpdateTeamAsync(id, updateTeamDto, userId);
                return Ok(team);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新团队时发生错误，团队ID: {TeamId}", id);
                return StatusCode(500, "更新团队失败");
            }
        }

        /// <summary>
        /// 删除团队（需要登录且为团队拥有者或管理员）
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> DeleteTeam(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("用户未登录");
                }

                // 检查是否为管理员
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var isAdmin = userRole == UserRole.Admin.ToString() || userRole == UserRole.SuperAdmin.ToString();

                var result = await _teamService.DeleteTeamAsync(id, userId, isAdmin);
                if (!result)
                {
                    return NotFound("团队不存在");
                }

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除团队时发生错误，团队ID: {TeamId}", id);
                return StatusCode(500, "删除团队失败");
            }
        }

        /// <summary>
        /// 绑定团队（需要登录）
        /// </summary>
        [HttpPost("bind")]
        [Authorize]
        public async Task<ActionResult> BindTeam([FromBody] TeamBindDto bindDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("用户未登录");
                }

                var result = await _teamService.BindTeamAsync(bindDto.TeamId, bindDto.Password, userId);
                if (!result)
                {
                    return BadRequest("团队不存在或密码错误");
                }

                return Ok(new { message = "团队绑定成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "绑定团队时发生错误，团队ID: {TeamId}", bindDto.TeamId);
                return StatusCode(500, "绑定团队失败");
            }
        }

        /// <summary>
        /// 修改团队密码（需要登录且为团队拥有者）
        /// </summary>
        [HttpPost("{id}/change-password")]
        [Authorize]
        public async Task<ActionResult> ChangeTeamPassword(Guid id, [FromBody] ChangeTeamPasswordDto changePasswordDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("用户未登录");
                }

                var result = await _teamService.ChangeTeamPasswordAsync(id, changePasswordDto, userId);
                if (!result)
                {
                    return NotFound("团队不存在");
                }

                return Ok(new { message = "密码修改成功" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "修改团队密码时发生错误，团队ID: {TeamId}", id);
                return StatusCode(500, "修改密码失败");
            }
        }

        /// <summary>
        /// 验证团队所有权（需要登录）
        /// </summary>
        [HttpGet("{id}/verify-ownership")]
        [Authorize]
        public async Task<ActionResult<bool>> VerifyTeamOwnership(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("用户未登录");
                }

                var result = await _teamService.VerifyTeamOwnershipAsync(id, userId);
                return Ok(new { isOwner = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证团队所有权时发生错误，团队ID: {TeamId}", id);
                return StatusCode(500, "验证失败");
            }
        }

        /// <summary>
        /// 给团队点赞
        /// </summary>
        [HttpPost("{id}/like")]
        public async Task<ActionResult> LikeTeam(Guid id)
        {
            try
            {
                var likes = await _teamService.LikeTeamAsync(id);
                return Ok(new { likes });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "给团队点赞时发生错误，团队ID: {TeamId}", id);
                return StatusCode(500, "点赞失败");
            }
        }
    }
}