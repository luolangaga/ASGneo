namespace ASG.Api.DTOs
{
    public class ConversationDto
    {
        public Guid Id { get; set; }
        public string? PeerUserId { get; set; }
        public string? PeerName { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? LastTime { get; set; }
        public int UnreadCount { get; set; }
    }
}