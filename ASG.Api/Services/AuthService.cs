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
            var existingUser = await _userManager.FindByEmailAsync(registrationDto.Email);
            if (existingUser != null)
            {
                return null; // User already exists
            }

            var user = new User
            {
                UserName = registrationDto.Email,
                Email = registrationDto.Email,
                FirstName = registrationDto.FirstName,
                LastName = registrationDto.LastName,
                Role = registrationDto.Role,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, registrationDto.Password);
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
                    IsActive = user.IsActive
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
                    IsActive = user.IsActive
                }
            };
        }

        public async Task<bool> LogoutAsync(string userId)
        {
            // In JWT implementation, logout is typically handled client-side
            // by removing the token. Server-side logout would require token blacklisting
            // which is not implemented in this basic version.
            await Task.CompletedTask;
            return true;
        }
    }
}