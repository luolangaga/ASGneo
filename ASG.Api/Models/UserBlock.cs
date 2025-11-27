namespace ASG.Api.Models
{
    public class UserBlock
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string BlockerUserId { get; set; } = string.Empty;
        public string BlockedUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}