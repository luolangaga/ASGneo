namespace ASG.Api.Services
{
    public interface IEmailService
    {
        Task<bool> SendAsync(string to, string subject, string body);
        Task<bool> SendHtmlAsync(string to, string subject, string htmlBody, string? plainTextBody = null);
    }
}