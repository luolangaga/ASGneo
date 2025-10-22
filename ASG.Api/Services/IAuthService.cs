using ASG.Api.DTOs;

namespace ASG.Api.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> RegisterAsync(UserRegistrationDto registrationDto);
        Task<AuthResponseDto?> LoginAsync(UserLoginDto loginDto);
        Task<bool> LogoutAsync(string userId);
    }
}