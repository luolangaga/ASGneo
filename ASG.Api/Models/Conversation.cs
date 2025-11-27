using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    public class Conversation
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public bool IsDirect { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}