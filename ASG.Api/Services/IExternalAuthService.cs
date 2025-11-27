using ASG.Api.DTOs;

namespace ASG.Api.Services
{
    public interface IExternalAuthService
    {
        string GetAuthorizeUrl(string provider, string redirect);
        string GetAuthorizeUrlWithCallback(string provider, string redirect, string callbackUrl);
        Task<AuthResponseDto?> ProcessCallbackAsync(string provider, string code, string? state, string callbackBaseUrl);
        Task<bool> LinkProviderAsync(string provider, string code, string callbackUrl, string currentUserId);
    }
}
