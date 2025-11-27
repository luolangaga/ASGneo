using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    public class Message
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public Guid ConversationId { get; set; }
        [Required]
        public string SenderUserId { get; set; } = string.Empty;
        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
    }
}