using ASG.Api.DTOs;
using Microsoft.AspNetCore.Identity;

namespace ASG.Api.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> RegisterAsync(UserRegistrationDto registrationDto);
        Task<AuthResponseDto?> LoginAsync(UserLoginDto loginDto);
        Task<bool> LogoutAsync(string userId);
        Task<bool> RequestPasswordResetAsync(string email);
        Task<bool> ResetPasswordAsync(string email, string token, string newPassword);
    }
}