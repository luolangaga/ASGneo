using ASG.Api.Data;
using ASG.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<ASG.Api.Hubs.AppHub> _hub;

        public NotificationsController(ApplicationDbContext db, IHubContext<ASG.Api.Hubs.AppHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<NotificationDto>>> List()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            var list = _db.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).Take(100).Select(n => new NotificationDto { Id = n.Id, UserId = n.UserId, Type = n.Type, Payload = n.Payload, IsRead = n.IsRead, CreatedAt = n.CreatedAt });
            return Ok(await Task.FromResult(list.ToList()));
        }

        [HttpPost("{id}/read")]
        public async Task<IActionResult> Read(Guid id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            var n = _db.Notifications.FirstOrDefault(x => x.Id == id && x.UserId == userId);
            if (n == null) return NotFound(new { message = "不存在" });
            n.IsRead = true;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> ReadAll()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            var all = _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToList();
            foreach (var n in all) n.IsRead = true;
            if (all.Count > 0) await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        public class PublishDto
        {
            public string ToUserId { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string? Payload { get; set; }
        }

        [HttpPost("publish")]
        public async Task<IActionResult> Publish([FromBody] PublishDto dto)
        {
            var fromUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(fromUserId)) return Unauthorized(new { message = "未登录" });
            if (string.IsNullOrWhiteSpace(dto?.ToUserId) || string.IsNullOrWhiteSpace(dto?.Type)) return BadRequest(new { message = "参数错误" });
            var n = new ASG.Api.Models.Notification
            {
                Id = Guid.NewGuid(),
                UserId = dto.ToUserId,
                Type = dto.Type,
                Payload = dto.Payload ?? string.Empty,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            };
            _db.Notifications.Add(n);
            await _db.SaveChangesAsync();
            var payload = new ASG.Api.DTOs.NotificationDto { Id = n.Id, UserId = n.UserId, Type = n.Type, Payload = n.Payload, IsRead = n.IsRead, CreatedAt = n.CreatedAt };
            await _hub.Clients.Groups($"user:{dto.ToUserId}").SendAsync("ReceiveNotification", payload);
            return Ok(new { success = true, id = n.Id });
        }
    }
}
