using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    public class ConversationMember
    {
        [Required]
        public Guid ConversationId { get; set; }
        [Required]
        public string UserId { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
    }
}