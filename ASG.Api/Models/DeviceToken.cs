namespace ASG.Api.Models
{
    public class DeviceToken
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty; // ios/android/web
        public DateTime CreatedAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
