using ASG.Api.DTOs;
using ASG.Api.Authorization;
using ASG.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    /// <summary>
    /// 赛程管理控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MatchesController : ControllerBase
    {
        private readonly IMatchService _matchService;

        public MatchesController(IMatchService matchService)
        {
            _matchService = matchService;
        }

        /// <summary>
        /// 获取所有赛程，支持分页和按赛事过滤
        /// </summary>
        /// <param name="eventId">赛事ID（可选）</param>
        /// <param name="page">页码（默认1）</param>
        /// <param name="pageSize">每页大小（默认10）</param>
        /// <returns>赛程列表</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MatchDto>>> GetAllMatches([FromQuery] Guid? eventId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var matches = await _matchService.GetAllMatchesAsync(eventId, page, pageSize);
                var total = await _matchService.GetMatchCountAsync(eventId);
                Response.Headers["X-Total-Count"] = total.ToString();
                return Ok(matches);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取赛程列表失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 根据ID获取赛程
        /// </summary>
        /// <param name="id">赛程ID</param>
        /// <returns>赛程信息</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<MatchDto>> GetMatch(Guid id)
        {
            try
            {
                var matchDto = await _matchService.GetMatchByIdAsync(id);
                if (matchDto == null)
                {
                    return NotFound(new { message = "赛程不存在" });
                }

                return Ok(matchDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取赛程信息失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 创建赛程（需要登录；赛事创建者或管理员）
        /// </summary>
        /// <param name="createDto">创建赛程DTO</param>
        /// <returns>创建的赛程信息</returns>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<MatchDto>> CreateMatch([FromBody] CreateMatchDto createDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var matchDto = await _matchService.CreateMatchAsync(createDto, userId);
                return CreatedAtAction(nameof(GetMatch), new { id = matchDto.Id }, matchDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "创建赛程失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 更新赛程（需要登录；赛事创建者或管理员）
        /// </summary>
        /// <param name="id">赛程ID</param>
        /// <param name="updateDto">更新赛程DTO</param>
        /// <returns>更新的赛程信息</returns>
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<MatchDto>> UpdateMatch(Guid id, [FromBody] UpdateMatchDto updateDto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var matchDto = await _matchService.UpdateMatchAsync(id, updateDto, userId);
                if (matchDto == null)
                {
                    return NotFound(new { message = "赛程不存在" });
                }

                return Ok(matchDto);
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
                return StatusCode(500, new { message = "更新赛程失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 删除赛程（需要登录；赛事创建者或管理员）
        /// </summary>
        /// <param name="id">赛程ID</param>
        /// <returns>删除结果</returns>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> DeleteMatch(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var result = await _matchService.DeleteMatchAsync(id, userId);
                if (!result)
                {
                    return NotFound(new { message = "赛程不存在" });
                }

                return Ok(new { message = "赛程删除成功" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "删除赛程失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 给赛程点赞
        /// </summary>
        /// <param name="id">赛程ID</param>
        /// <returns>更新后的点赞数</returns>
        [HttpPost("{id}/like")]
        public async Task<ActionResult<int>> LikeMatch(Guid id)
        {
            try
            {
                var likes = await _matchService.LikeMatchAsync(id);
                return Ok(likes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "点赞失败", error = ex.Message });
            }
        }
    }
}