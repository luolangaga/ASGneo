using ASG.Api.Data;
using ASG.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public DevicesController(ApplicationDbContext db)
        {
            _db = db;
        }

        public class RegisterDto
        {
            public string Token { get; set; } = string.Empty;
            public string Platform { get; set; } = string.Empty;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            if (string.IsNullOrWhiteSpace(dto?.Token)) return BadRequest(new { message = "Token 为空" });
            var exists = await _db.DeviceTokens.FirstOrDefaultAsync(x => x.UserId == userId && x.Token == dto.Token);
            if (exists == null)
            {
                _db.DeviceTokens.Add(new DeviceToken { UserId = userId, Token = dto.Token, Platform = dto.Platform ?? string.Empty, CreatedAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow, IsActive = true });
            }
            else
            {
                exists.Platform = dto.Platform ?? exists.Platform;
                exists.IsActive = true;
                exists.LastSeenAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}
