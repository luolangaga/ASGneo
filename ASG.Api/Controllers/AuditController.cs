using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly ILogger<AuditController> _logger;

        public AuditController(ILogger<AuditController> logger)
        {
            _logger = logger;
        }

        public class QqAuditDto
        {
            public Guid EventId { get; set; }
            public Guid TeamId { get; set; }
            public string Action { get; set; } = string.Empty; // view/copy
        }

        [HttpPost("qq")]
        [Authorize]
        public IActionResult AuditQq([FromBody] QqAuditDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var when = DateTime.UtcNow.ToString("O");
            _logger.LogInformation("AUDIT QQ {Action}: EventId={EventId}, TeamId={TeamId}, UserId={UserId}, Time={Time}", dto.Action, dto.EventId, dto.TeamId, userId, when);
            return Ok(new { message = "ok" });
        }
    }
}

