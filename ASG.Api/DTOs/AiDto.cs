namespace ASG.Api.DTOs
{
    public class GenerateLogoRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class GenerateLogoResponseDto
    {
        public string Url { get; set; } = string.Empty;
    }

    public class PolishTextRequestDto
    {
        public string Scope { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class PolishTextResponseDto
    {
        public string Text { get; set; } = string.Empty;
    }

    public class AiCommandRequestDto
    {
        public string Command { get; set; } = string.Empty;
    }

    public class AiCommandActionResult
    {
        public string Action { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }
    }

    public class AiCommandResponseDto
    {
        public List<AiCommandActionResult> Results { get; set; } = new List<AiCommandActionResult>();
    }
}
