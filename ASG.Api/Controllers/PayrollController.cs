using ASG.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PayrollController : ControllerBase
    {
        private readonly IPayrollService _svc;
        public PayrollController(IPayrollService svc) { _svc = svc; }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<DTOs.PayrollEntryDto>>> Get([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] string? userId = null)
        {
            var me = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(me)) return Unauthorized(new { message = "未登录" });
            var target = string.IsNullOrWhiteSpace(userId) ? me : userId!;
            try { return Ok(await _svc.GetPayrollAsync(target, from, to)); } catch (Exception ex) { return StatusCode(500, new { message = "获取失败", error = ex.Message }); }
        }
    }
}
