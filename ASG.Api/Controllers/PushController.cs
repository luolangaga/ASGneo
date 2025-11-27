using ASG.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PushController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpFactory;

        public PushController(ApplicationDbContext db, IHttpClientFactory httpFactory)
        {
            _db = db;
            _httpFactory = httpFactory;
        }

        public class PushDto
        {
            public string? Title { get; set; }
            public string? Body { get; set; }
            public string? Route { get; set; }
            public string? ToUserId { get; set; }
        }

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] PushDto dto)
        {
            var fromUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(fromUserId)) return Unauthorized(new { message = "未登录" });
            var userId = string.IsNullOrWhiteSpace(dto?.ToUserId) ? fromUserId : dto.ToUserId!;
            var tokens = await _db.DeviceTokens.Where(x => x.UserId == userId && x.IsActive).Select(x => x.Token).ToListAsync();
            if (tokens.Count == 0) return BadRequest(new { message = "未找到设备令牌" });
            var serverKey = Environment.GetEnvironmentVariable("FCM_SERVER_KEY");
            if (string.IsNullOrWhiteSpace(serverKey)) return StatusCode(500, new { message = "未配置 FCM_SERVER_KEY" });
            var client = _httpFactory.CreateClient();
            client.BaseAddress = new Uri("https://fcm.googleapis.com/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("key", serverKey);
            var success = 0; var fail = 0;
            foreach (var t in tokens)
            {
                var payload = new
                {
                    to = t,
                    notification = new { title = dto?.Title ?? "ASG", body = dto?.Body ?? "收到新通知" },
                    data = new { route = dto?.Route ?? "/notifications" }
                };
                var req = new HttpRequestMessage(HttpMethod.Post, "fcm/send")
                {
                    Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
                };
                try
                {
                    var resp = await client.SendAsync(req);
                    if (resp.IsSuccessStatusCode) success++; else fail++;
                }
                catch { fail++; }
            }
            return Ok(new { success, fail });
        }
    }
}
