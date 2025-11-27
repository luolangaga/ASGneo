using Microsoft.AspNetCore.Mvc;
using ASG.Api.Services;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OAuthController : ControllerBase
    {
        private readonly IExternalAuthService _svc;
        private readonly IConfiguration _config;

        public OAuthController(IExternalAuthService svc, IConfiguration config)
        {
            _svc = svc;
            _config = config;
        }

        [HttpGet("{provider}/authorize")]
        public IActionResult Authorize(string provider, [FromQuery] string? redirect)
        {
            var url = _svc.GetAuthorizeUrl(provider, redirect ?? "/");
            if (string.IsNullOrEmpty(url)) return BadRequest(new { message = "不支持的提供方" });
            return Ok(new { url });
        }

        [HttpGet("{provider}/callback")]
        public async Task<IActionResult> Callback(string provider, [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, [FromQuery] string? error_description)
        {
            var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? _config["Urls"] ?? "http://localhost:5250";
            var baseUrl = urls.Split(';').FirstOrDefault() ?? urls;
            var frontend = _config["Frontend:BaseUrl"] ?? _config["BaseUrl"] ?? "http://localhost:5175";
            var redirect = "/";
            if (!string.IsNullOrWhiteSpace(state))
            {
                try { redirect = Encoding.UTF8.GetString(Convert.FromBase64String(state)); } catch { }
            }
            if (frontend.EndsWith("/")) frontend = frontend.TrimEnd('/');

            if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code))
            {
                var msg = !string.IsNullOrWhiteSpace(error_description) ? error_description : "登录失败或用户取消授权";
                var failUrl = $"{frontend}/login?error={Uri.EscapeDataString(msg)}&redirect={Uri.EscapeDataString(redirect)}";
                return Redirect(failUrl);
            }

            var res = await _svc.ProcessCallbackAsync(provider, code!, state, baseUrl);
            if (res == null)
            {
                var failUrl = $"{frontend}/login?error={Uri.EscapeDataString("登录失败")}&redirect={Uri.EscapeDataString(redirect)}";
                return Redirect(failUrl);
            }
            var goNew = res.IsNewUser || string.IsNullOrWhiteSpace(res.User.FullName);
            var nextUrl = goNew
                ? $"{frontend}/oauth/new-account?token={Uri.EscapeDataString(res.Token)}&provider={Uri.EscapeDataString(provider)}&redirect={Uri.EscapeDataString(redirect)}"
                : $"{frontend}/login/callback?token={Uri.EscapeDataString(res.Token)}&redirect={Uri.EscapeDataString(redirect)}";
            return Redirect(nextUrl);
        }

        [Authorize]
        [HttpGet("{provider}/link/authorize")]
        public IActionResult LinkAuthorize(string provider, [FromQuery] string? redirect)
        {
            var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? _config["Urls"] ?? "http://localhost:5250";
            var baseUrl = urls.Split(';').FirstOrDefault() ?? urls;
            var uid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{uid}|{(redirect ?? "/")}"));
            var linkCallback = new Uri(new Uri(baseUrl), $"/api/oauth/{provider}/link/callback").ToString();
            var url = _svc.GetAuthorizeUrlWithCallback(provider, payload, linkCallback);
            if (string.IsNullOrEmpty(url)) return BadRequest(new { message = "不支持的提供方" });
            return Ok(new { url });
        }

        [HttpGet("{provider}/link/callback")]
        public async Task<IActionResult> LinkCallback(string provider, [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, [FromQuery] string? error_description)
        {
            var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? _config["Urls"] ?? "http://localhost:5250";
            var baseUrl = urls.Split(';').FirstOrDefault() ?? urls;
            var frontend = _config["Frontend:BaseUrl"] ?? _config["BaseUrl"] ?? "http://localhost:5175";
            if (frontend.EndsWith("/")) frontend = frontend.TrimEnd('/');

            string uid = "";
            string redirect = "/";
            if (!string.IsNullOrWhiteSpace(state))
            {
                try
                {
                    var raw = Encoding.UTF8.GetString(Convert.FromBase64String(state));
                    var parts = raw.Split('|');
                    uid = parts.ElementAtOrDefault(0) ?? "";
                    redirect = parts.ElementAtOrDefault(1) ?? "/";
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(uid))
            {
                var msg = !string.IsNullOrWhiteSpace(error_description) ? error_description : "绑定失败或取消授权";
                var failUrl = $"{frontend}/profile?link=fail&message={Uri.EscapeDataString(msg)}";
                return Redirect(failUrl);
            }

            var linkCb = new Uri(new Uri(baseUrl), $"/api/oauth/{provider}/link/callback").ToString();
            var ok = await _svc.LinkProviderAsync(provider, code!, linkCb, uid);
            var url = ok ? $"{frontend}/profile?link=success&redirect={Uri.EscapeDataString(redirect)}" : $"{frontend}/profile?link=fail";
            return Redirect(url);
        }
    }
}
