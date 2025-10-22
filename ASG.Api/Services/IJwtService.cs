using ASG.Api.Models;

namespace ASG.Api.Services
{
    public interface IJwtService
    {
        string GenerateToken(User user);
        string? ValidateToken(string token);
    }
}