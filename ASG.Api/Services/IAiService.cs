namespace ASG.Api.Services
{
    public interface IAiService
    {
        Task<byte[]?> GenerateLogoAsync(string name, string description, CancellationToken ct);
        Task<string?> PolishTextAsync(string scope, string text, CancellationToken ct);
        Task<List<AiService.ToolCall>?> PlanToolsAsync(string command, CancellationToken ct);
        Task<List<AiService.ToolCall>?> PlanToolsAsync(string command, List<ASG.Api.DTOs.AiCommandActionResult> previousResults, CancellationToken ct);
    }
}
