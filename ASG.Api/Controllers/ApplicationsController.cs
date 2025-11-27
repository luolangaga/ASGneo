using ASG.Api.DTOs;
using ASG.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApplicationsController : ControllerBase
    {
        private readonly IApplicationService _svc;
        public ApplicationsController(IApplicationService svc) { _svc = svc; }

        [HttpPost("{id}/approve")]
        [Authorize]
        public async Task<ActionResult<RecruitmentApplicationDto>> Approve(Guid id)
        { var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" }); try { var d = await _svc.ApproveAsync(id, userId); if (d == null) return NotFound(new { message = "不存在" }); return Ok(d); } catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); } catch (Exception ex) { return StatusCode(500, new { message = "操作失败", error = ex.Message }); } }

        [HttpPost("{id}/reject")]
        [Authorize]
        public async Task<ActionResult<RecruitmentApplicationDto>> Reject(Guid id)
        { var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" }); try { var d = await _svc.RejectAsync(id, userId); if (d == null) return NotFound(new { message = "不存在" }); return Ok(d); } catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); } catch (Exception ex) { return StatusCode(500, new { message = "操作失败", error = ex.Message }); } }

        [HttpPost("{id}/sync-matches")]
        [Authorize]
        public async Task<ActionResult> SyncMatches(Guid id, [FromBody] SyncMatchesDto dto)
        { var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" }); try { var ok = await _svc.SyncMatchesAsync(id, dto, userId); if (!ok) return NotFound(new { message = "不存在" }); return Ok(new { message = "同步完成" }); } catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); } catch (Exception ex) { return StatusCode(500, new { message = "同步失败", error = ex.Message }); } }

        [HttpGet("my")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<RecruitmentApplicationDto>>> My()
        { var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" }); try { return Ok(await _svc.GetMyApplicationsAsync(userId)); } catch (Exception ex) { return StatusCode(500, new { message = "获取失败", error = ex.Message }); } }
    }
}
