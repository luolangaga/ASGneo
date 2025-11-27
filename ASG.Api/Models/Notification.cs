using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    public class Notification
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public string UserId { get; set; } = string.Empty;
        [Required]
        [MaxLength(100)]
        public string Type { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}