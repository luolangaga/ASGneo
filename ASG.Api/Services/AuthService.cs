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

        public AuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IJwtService jwtService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
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
                FirstName = registrationDto.FirstName,
                LastName = registrationDto.LastName,
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
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    FullName = user.FullName,
                    Role = user.Role,
                    RoleDisplayName = user.RoleDisplayName,
                    RoleName = user.RoleName,
                    CreatedAt = user.CreatedAt,
                    IsActive = user.IsActive,
                    TeamId = user.TeamId
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
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    FullName = user.FullName,
                    Role = user.Role,
                    RoleDisplayName = user.RoleDisplayName,
                    RoleName = user.RoleName,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    IsActive = user.IsActive,
                    TeamId = user.TeamId
                }
            };
        }

        public async Task<bool> LogoutAsync(string userId)
        {
           
            await Task.CompletedTask;
            return true;
        }
    }
}