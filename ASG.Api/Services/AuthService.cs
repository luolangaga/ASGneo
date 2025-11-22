using Microsoft.AspNetCore.Identity;
using ASG.Api.DTOs;
using ASG.Api.Models;

namespace ASG.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;

        public AuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IJwtService jwtService,
            IEmailService emailService,
            IConfiguration config)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
            _emailService = emailService;
            _config = config;
        }

        public async Task<AuthResponseDto?> RegisterAsync(UserRegistrationDto registrationDto)
        {
            // 规范化邮箱，避免因空格或大小写导致误判
            var email = (registrationDto.Email ?? string.Empty).Trim();

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                throw new InvalidOperationException("邮箱已被使用。");
            }

            var user = new User
            {
                UserName = email,
                Email = email,
                FirstName = registrationDto.FullName,
                LastName = string.Empty,
                Role = registrationDto.Role,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, registrationDto.Password);
            if (!result.Succeeded)
            {
                var errorMessage = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"注册失败: {errorMessage}");
            }

            var token = _jwtService.GenerateToken(user);

            return new AuthResponseDto
            {
                Token = token,
                Expires = DateTime.UtcNow.AddMinutes(60), // 应与 JWT 设置匹配
                User = new UserResponseDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = user.Role,
                    RoleDisplayName = user.RoleDisplayName,
                    RoleName = user.RoleName,
                    CreatedAt = user.CreatedAt,
                    IsActive = user.IsActive,
                    TeamId = user.TeamId,
                    EmailCredits = user.EmailCredits
                }
            };
        }

        public async Task<AuthResponseDto?> LoginAsync(UserLoginDto loginDto)
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email);
            if (user == null || !user.IsActive)
            {
                return null;
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);
            if (!result.Succeeded)
            {
                return null;
            }

            var token = _jwtService.GenerateToken(user);

            return new AuthResponseDto
            {
                Token = token,
                Expires = DateTime.UtcNow.AddMinutes(60), // Should match JWT settings
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
                }
            };
        }

        public async Task<bool> LogoutAsync(string userId)
        {
           
            await Task.CompletedTask;
            return true;
        }

        public async Task<bool> RequestPasswordResetAsync(string email)
        {
            var targetEmail = (email ?? string.Empty).Trim();
            var user = await _userManager.FindByEmailAsync(targetEmail);
            if (user == null)
            {
                return true;
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var baseUrl = _config["Frontend:BaseUrl"] ?? "https://idvevent.cn";
            if (baseUrl.EndsWith("/")) baseUrl = baseUrl.TrimEnd('/');
            var resetPath = _config["Frontend:ResetPasswordPath"] ?? "/reset-password";
            var link = $"{baseUrl}{resetPath}?email={Uri.EscapeDataString(targetEmail)}&token={Uri.EscapeDataString(token)}";
            var html = $"<p>请点击以下链接重置密码：</p><p><a href=\"{link}\">{link}</a></p><p>如果无法打开链接，可复制令牌至重置页面：</p><pre style=\"white-space:pre-wrap;word-break:break-all\">{System.Net.WebUtility.HtmlEncode(token)}</pre>";
            await _emailService.SendHtmlAsync(targetEmail, "密码重置", html, token);
            return true;
        }

        public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
        {
            var targetEmail = (email ?? string.Empty).Trim();
            var user = await _userManager.FindByEmailAsync(targetEmail);
            if (user == null)
            {
                return false;
            }

            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            return result.Succeeded;
        }

    }
}