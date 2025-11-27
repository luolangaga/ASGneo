using ASG.Api.DTOs;
using ASG.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecruitmentsController : ControllerBase
    {
        private readonly IRecruitmentService _svc;
        public RecruitmentsController(IRecruitmentService svc) { _svc = svc; }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RecruitmentDto>>> List([FromQuery] Guid? eventId = null, [FromQuery] string? position = null, [FromQuery] string? q = null, [FromQuery] bool includeClosed = false)
        { try { return Ok(await _svc.GetRecruitmentsAsync(eventId, position, q, includeClosed)); } catch (Exception ex) { return StatusCode(500, new { message = "获取失败", error = ex.Message }); } }

        [HttpGet("{id}")]
        public async Task<ActionResult<RecruitmentDto>> Get(Guid id)
        { try { var d = await _svc.GetRecruitmentAsync(id); if (d == null) return NotFound(new { message = "不存在" }); return Ok(d); } catch (Exception ex) { return StatusCode(500, new { message = "获取失败", error = ex.Message }); } }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<RecruitmentDto>> Create([FromBody] CreateRecruitmentDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            try { var d = await _svc.CreateRecruitmentAsync(dto, userId); return CreatedAtAction(nameof(Get), new { id = d.Id }, d); } catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); } catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); } catch (Exception ex) { return StatusCode(500, new { message = "创建失败", error = ex.Message }); }
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<RecruitmentDto>> Update(Guid id, [FromBody] UpdateRecruitmentDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            try { var d = await _svc.UpdateRecruitmentAsync(id, dto, userId); if (d == null) return NotFound(new { message = "不存在" }); return Ok(d); } catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); } catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); } catch (Exception ex) { return StatusCode(500, new { message = "更新失败", error = ex.Message }); }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> Delete(Guid id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            try { var ok = await _svc.DeleteRecruitmentAsync(id, userId); if (!ok) return NotFound(new { message = "不存在" }); return Ok(new { message = "删除成功" }); } catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); } catch (Exception ex) { return StatusCode(500, new { message = "删除失败", error = ex.Message }); }
        }

        [HttpGet("{id}/applications")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<RecruitmentApplicationDto>>> ListApplications(Guid id)
        { var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" }); try { return Ok(await _svc.GetApplicationsAsync(id, userId)); } catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); } catch (Exception ex) { return StatusCode(500, new { message = "获取失败", error = ex.Message }); } }

        [HttpPost("{id}/apply")]
        [Authorize]
        public async Task<ActionResult<RecruitmentApplicationDto>> Apply(Guid id, [FromBody] ApplyRecruitmentDto dto)
        { var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" }); try { var d = await _svc.ApplyAsync(id, dto, userId); return Ok(d); } catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); } catch (Exception ex) { return StatusCode(500, new { message = "提交失败", error = ex.Message }); } }
    }
}
