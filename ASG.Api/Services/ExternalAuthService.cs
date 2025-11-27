using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ASG.Api.DTOs;
using ASG.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace ASG.Api.Services
{
    public class ExternalAuthService : IExternalAuthService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly UserManager<User> _userManager;
        private readonly IJwtService _jwtService;

        public ExternalAuthService(IHttpClientFactory httpFactory, IConfiguration config, UserManager<User> userManager, IJwtService jwtService)
        {
            _httpFactory = httpFactory;
            _config = config;
            _userManager = userManager;
            _jwtService = jwtService;
        }

        public string GetAuthorizeUrl(string provider, string redirect)
        {
            var cb = BuildCallbackUrl(provider);
            var state = string.IsNullOrWhiteSpace(redirect) ? "" : Convert.ToBase64String(Encoding.UTF8.GetBytes(redirect));
            if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
            {
                var clientId = _config["OAuth:GitHub:ClientId"] ?? string.Empty;
                var scope = _config["OAuth:GitHub:Scope"] ?? "user:email";
                return $"https://github.com/login/oauth/authorize?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(cb)}&scope={Uri.EscapeDataString(scope)}&state={Uri.EscapeDataString(state)}";
            }
            if (provider.Equals("microsoft", StringComparison.OrdinalIgnoreCase))
            {
                var clientId = _config["OAuth:Microsoft:ClientId"] ?? string.Empty;
                var scope = _config["OAuth:Microsoft:Scope"] ?? "openid profile email";
                return $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id={Uri.EscapeDataString(clientId)}&response_type=code&redirect_uri={Uri.EscapeDataString(cb)}&scope={Uri.EscapeDataString(scope)}&state={Uri.EscapeDataString(state)}&response_mode=query";
            }
            if (provider.Equals("qq", StringComparison.OrdinalIgnoreCase))
            {
                var appId = _config["OAuth:QQ:ClientId"] ?? string.Empty;
                var scope = _config["OAuth:QQ:Scope"] ?? "get_user_info";
                return $"https://graph.qq.com/oauth2.0/authorize?response_type=code&client_id={Uri.EscapeDataString(appId)}&redirect_uri={Uri.EscapeDataString(cb)}&scope={Uri.EscapeDataString(scope)}&state={Uri.EscapeDataString(state)}";
            }
            return string.Empty;
        }

        public string GetAuthorizeUrlWithCallback(string provider, string redirect, string callbackUrl)
        {
            var cb = callbackUrl;
            var state = string.IsNullOrWhiteSpace(redirect) ? "" : Convert.ToBase64String(Encoding.UTF8.GetBytes(redirect));
            if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
            {
                var clientId = _config["OAuth:GitHub:ClientId"] ?? string.Empty;
                var scope = _config["OAuth:GitHub:Scope"] ?? "user:email";
                return $"https://github.com/login/oauth/authorize?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(cb)}&scope={Uri.EscapeDataString(scope)}&state={Uri.EscapeDataString(state)}";
            }
            if (provider.Equals("microsoft", StringComparison.OrdinalIgnoreCase))
            {
                var clientId = _config["OAuth:Microsoft:ClientId"] ?? string.Empty;
                var scope = _config["OAuth:Microsoft:Scope"] ?? "openid profile email";
                return $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id={Uri.EscapeDataString(clientId)}&response_type=code&redirect_uri={Uri.EscapeDataString(cb)}&scope={Uri.EscapeDataString(scope)}&state={Uri.EscapeDataString(state)}&response_mode=query";
            }
            if (provider.Equals("qq", StringComparison.OrdinalIgnoreCase))
            {
                var appId = _config["OAuth:QQ:ClientId"] ?? string.Empty;
                var scope = _config["OAuth:QQ:Scope"] ?? "get_user_info";
                return $"https://graph.qq.com/oauth2.0/authorize?response_type=code&client_id={Uri.EscapeDataString(appId)}&redirect_uri={Uri.EscapeDataString(cb)}&scope={Uri.EscapeDataString(scope)}&state={Uri.EscapeDataString(state)}";
            }
            return string.Empty;
        }

        public async Task<AuthResponseDto?> ProcessCallbackAsync(string provider, string code, string? state, string callbackBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
            {
                var token = await ExchangeGithubTokenAsync(code);
                if (string.IsNullOrEmpty(token)) return null;
                var info = await FetchGithubUserAsync(token);
                if (info == null) return null;
                var (id, login, name, email) = info.Value;
                var finalEmail = string.IsNullOrWhiteSpace(email) ? (await FetchGithubPrimaryEmailAsync(token) ?? $"github-{id}@oauth.local") : email;
                var displayName = name ?? login ?? "GitHubUser";
                var res = await EnsureUserAndIssueTokenAsync("github", id, displayName, finalEmail);
                return res;
            }
            if (provider.Equals("microsoft", StringComparison.OrdinalIgnoreCase))
            {
                var token = await ExchangeMicrosoftTokenAsync(code);
                if (string.IsNullOrEmpty(token)) return null;
                var info = await FetchMicrosoftUserAsync(token);
                if (info == null) return null;
                var (sub, name, email, preferred_username, given_name) = info.Value;
                var finalEmail = email ?? preferred_username ?? $"ms-{sub}@oauth.local";
                var displayName = name ?? given_name ?? "MicrosoftUser";
                var res = await EnsureUserAndIssueTokenAsync("microsoft", sub, displayName, finalEmail);
                return res;
            }
            if (provider.Equals("qq", StringComparison.OrdinalIgnoreCase))
            {
                var token = await ExchangeQqTokenAsync(code);
                if (string.IsNullOrEmpty(token)) return null;
                var openId = await FetchQqOpenIdAsync(token);
                if (string.IsNullOrEmpty(openId)) return null;
                var nickname = await FetchQqUserAsync(token, openId);
                var name = nickname ?? "QQUser";
                var email = $"qq-{openId}@oauth.local";
                var res = await EnsureUserAndIssueTokenAsync("qq", openId, name, email);
                return res;
            }
            return null;
        }

        private string BuildCallbackUrl(string provider)
        {
            var scheme = "http";
            var host = _config["Self:Host"] ?? string.Empty;
            var frontend = _config["Frontend:BaseUrl"] ?? string.Empty;
            var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? _config["Urls"] ?? "http://localhost:5250";
            var baseUrl = urls.Split(';').FirstOrDefault() ?? urls;
            var uri = new Uri(baseUrl);
            scheme = uri.Scheme;
            host = uri.Host + (uri.IsDefaultPort ? "" : $":{uri.Port}");
            return $"{scheme}://{host}/api/oauth/{provider}/callback";
        }

        private async Task<string?> ExchangeGithubTokenAsync(string code)
        {
            var clientId = _config["OAuth:GitHub:ClientId"] ?? string.Empty;
            var clientSecret = _config["OAuth:GitHub:ClientSecret"] ?? string.Empty;
            var cb = BuildCallbackUrl("github");
            var http = _httpFactory.CreateClient();
            var body = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["redirect_uri"] = cb
            };
            var resp = await http.PostAsync("https://github.com/login/oauth/access_token", new FormUrlEncodedContent(body));
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return null;
            try
            {
                var doc = JsonSerializer.Deserialize<Dictionary<string, object>>(text);
                if (doc != null && doc.TryGetValue("access_token", out var v)) return v?.ToString();
                if (text.Contains("access_token="))
                {
                    var q = System.Web.HttpUtility.ParseQueryString(text);
                    return q.Get("access_token");
                }
            }
            catch
            {
                if (text.Contains("access_token="))
                {
                    var q = System.Web.HttpUtility.ParseQueryString(text);
                    return q.Get("access_token");
                }
            }
            return null;
        }

        private async Task<string?> ExchangeGithubTokenAsync(string code, string callbackUrl)
        {
            var clientId = _config["OAuth:GitHub:ClientId"] ?? string.Empty;
            var clientSecret = _config["OAuth:GitHub:ClientSecret"] ?? string.Empty;
            var cb = callbackUrl;
            var http = _httpFactory.CreateClient();
            var body = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["redirect_uri"] = cb
            };
            var resp = await http.PostAsync("https://github.com/login/oauth/access_token", new FormUrlEncodedContent(body));
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return null;
            try
            {
                var doc = JsonSerializer.Deserialize<Dictionary<string, object>>(text);
                if (doc != null && doc.TryGetValue("access_token", out var v)) return v?.ToString();
                if (text.Contains("access_token="))
                {
                    var q = System.Web.HttpUtility.ParseQueryString(text);
                    return q.Get("access_token");
                }
            }
            catch
            {
                if (text.Contains("access_token="))
                {
                    var q = System.Web.HttpUtility.ParseQueryString(text);
                    return q.Get("access_token");
                }
            }
            return null;
        }

        private async Task<(string Id, string Login, string? Name, string? Email)?> FetchGithubUserAsync(string accessToken)
        {
            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("asg", "1.0"));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await http.GetAsync("https://api.github.com/user");
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var id = root.GetProperty("id").GetInt64().ToString();
            var login = root.GetProperty("login").GetString() ?? "";
            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
            return (id, login, name, email);
        }

        private async Task<string?> FetchGithubPrimaryEmailAsync(string accessToken)
        {
            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("asg", "1.0"));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await http.GetAsync("https://api.github.com/user/emails");
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var primary = el.TryGetProperty("primary", out var p) && p.GetBoolean();
                var verified = el.TryGetProperty("verified", out var v) && v.GetBoolean();
                var email = el.TryGetProperty("email", out var e) ? e.GetString() : null;
                if (primary && verified && !string.IsNullOrWhiteSpace(email)) return email;
            }
            return null;
        }

        private async Task<string?> ExchangeMicrosoftTokenAsync(string code)
        {
            var clientId = _config["OAuth:Microsoft:ClientId"] ?? string.Empty;
            var clientSecret = _config["OAuth:Microsoft:ClientSecret"] ?? string.Empty;
            var cb = BuildCallbackUrl("microsoft");
            var http = _httpFactory.CreateClient();
            var body = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["redirect_uri"] = cb,
                ["grant_type"] = "authorization_code"
            };
            var resp = await http.PostAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token", new FormUrlEncodedContent(body));
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("access_token").GetString();
            return token;
        }

        private async Task<string?> ExchangeMicrosoftTokenAsync(string code, string callbackUrl)
        {
            var clientId = _config["OAuth:Microsoft:ClientId"] ?? string.Empty;
            var clientSecret = _config["OAuth:Microsoft:ClientSecret"] ?? string.Empty;
            var cb = callbackUrl;
            var http = _httpFactory.CreateClient();
            var body = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["redirect_uri"] = cb,
                ["grant_type"] = "authorization_code"
            };
            var resp = await http.PostAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token", new FormUrlEncodedContent(body));
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("access_token").GetString();
            return token;
        }

        private async Task<(string sub, string? name, string? email, string? preferred_username, string? given_name)?> FetchMicrosoftUserAsync(string accessToken)
        {
            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await http.GetAsync("https://graph.microsoft.com/oidc/userinfo");
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var sub = root.GetProperty("sub").GetString() ?? string.Empty;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
            var preferred = root.TryGetProperty("preferred_username", out var pu) ? pu.GetString() : null;
            var given = root.TryGetProperty("given_name", out var gn) ? gn.GetString() : null;
            return (sub, name, email, preferred, given);
        }

        private async Task<string?> ExchangeQqTokenAsync(string code)
        {
            var appId = _config["OAuth:QQ:ClientId"] ?? string.Empty;
            var appKey = _config["OAuth:QQ:ClientSecret"] ?? string.Empty;
            var cb = BuildCallbackUrl("qq");
            var http = _httpFactory.CreateClient();
            var url = $"https://graph.qq.com/oauth2.0/token?grant_type=authorization_code&client_id={Uri.EscapeDataString(appId)}&client_secret={Uri.EscapeDataString(appKey)}&code={Uri.EscapeDataString(code)}&redirect_uri={Uri.EscapeDataString(cb)}&fmt=json";
            var resp = await http.GetAsync(url);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return null;
            try
            {
                using var doc = JsonDocument.Parse(text);
                return doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> ExchangeQqTokenAsync(string code, string callbackUrl)
        {
            var appId = _config["OAuth:QQ:ClientId"] ?? string.Empty;
            var appKey = _config["OAuth:QQ:ClientSecret"] ?? string.Empty;
            var cb = callbackUrl;
            var http = _httpFactory.CreateClient();
            var url = $"https://graph.qq.com/oauth2.0/token?grant_type=authorization_code&client_id={Uri.EscapeDataString(appId)}&client_secret={Uri.EscapeDataString(appKey)}&code={Uri.EscapeDataString(code)}&redirect_uri={Uri.EscapeDataString(cb)}&fmt=json";
            var resp = await http.GetAsync(url);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return null;
            try
            {
                using var doc = JsonDocument.Parse(text);
                return doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> FetchQqOpenIdAsync(string accessToken)
        {
            var http = _httpFactory.CreateClient();
            var url = $"https://graph.qq.com/oauth2.0/me?access_token={Uri.EscapeDataString(accessToken)}&fmt=json";
            var resp = await http.GetAsync(url);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return null;
            try
            {
                using var doc = JsonDocument.Parse(text);
                return doc.RootElement.TryGetProperty("openid", out var oid) ? oid.GetString() : null;
            }
            catch { return null; }
        }

        private async Task<string?> FetchQqUserAsync(string accessToken, string openId)
        {
            var appId = _config["OAuth:QQ:ClientId"] ?? string.Empty;
            var http = _httpFactory.CreateClient();
            var url = $"https://graph.qq.com/user/get_user_info?access_token={Uri.EscapeDataString(accessToken)}&oauth_consumer_key={Uri.EscapeDataString(appId)}&openid={Uri.EscapeDataString(openId)}";
            var resp = await http.GetAsync(url);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return null;
            try
            {
                using var doc = JsonDocument.Parse(text);
                var nickname = doc.RootElement.TryGetProperty("nickname", out var nn) ? nn.GetString() : null;
                return nickname;
            }
            catch { return null; }
        }

        private async Task<AuthResponseDto?> EnsureUserAndIssueTokenAsync(string provider, string providerKey, string name, string email)
        {
            var existing = await _userManager.FindByLoginAsync(provider, providerKey);
            User user;
            var createdNew = false;
            if (existing != null)
            {
                user = existing;
            }
            else
            {
                var byEmail = await _userManager.FindByEmailAsync(email);
                if (byEmail != null)
                {
                    user = byEmail;
                }
                else
                {
                    user = new User
                    {
                        UserName = email,
                        Email = email,
                        FirstName = name,
                        LastName = string.Empty,
                        Role = UserRole.User,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    var cr = await _userManager.CreateAsync(user);
                    if (!cr.Succeeded) return null;
                    createdNew = true;
                }
                await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));
            }

            var token = _jwtService.GenerateToken(user);
            return new AuthResponseDto
            {
                Token = token,
                Expires = DateTime.UtcNow.AddMinutes(60),
                User = new UserResponseDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = user.Role,
                    RoleDisplayName = user.RoleDisplayName,
                    RoleName = user.RoleName,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    IsActive = user.IsActive,
                    TeamId = user.TeamId,
                    EmailCredits = user.EmailCredits
                },
                IsNewUser = createdNew
            };
        }

        public async Task<bool> LinkProviderAsync(string provider, string code, string callbackUrl, string currentUserId)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            var user = await _userManager.FindByIdAsync(currentUserId);
            if (user == null) return false;
            if (provider.Equals("github", StringComparison.OrdinalIgnoreCase))
            {
                var token = await ExchangeGithubTokenAsync(code, callbackUrl);
                if (string.IsNullOrEmpty(token)) return false;
                var info = await FetchGithubUserAsync(token);
                if (info == null) return false;
                var (id, _, _, _) = info.Value;
                var r = await _userManager.AddLoginAsync(user, new UserLoginInfo("github", id, "github"));
                return r.Succeeded;
            }
            if (provider.Equals("microsoft", StringComparison.OrdinalIgnoreCase))
            {
                var token = await ExchangeMicrosoftTokenAsync(code, callbackUrl);
                if (string.IsNullOrEmpty(token)) return false;
                var info = await FetchMicrosoftUserAsync(token);
                if (info == null) return false;
                var (sub, _, _, _, _) = info.Value;
                var r = await _userManager.AddLoginAsync(user, new UserLoginInfo("microsoft", sub, "microsoft"));
                return r.Succeeded;
            }
            if (provider.Equals("qq", StringComparison.OrdinalIgnoreCase))
            {
                var token = await ExchangeQqTokenAsync(code, callbackUrl);
                if (string.IsNullOrEmpty(token)) return false;
                var openId = await FetchQqOpenIdAsync(token);
                if (string.IsNullOrEmpty(openId)) return false;
                var r = await _userManager.AddLoginAsync(user, new UserLoginInfo("qq", openId, "qq"));
                return r.Succeeded;
            }
            return false;
        }
    }
}
