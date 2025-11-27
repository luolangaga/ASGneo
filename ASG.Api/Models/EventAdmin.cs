using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASG.Api.Models
{
    public class EventAdmin
    {
        [Required]
        public Guid EventId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("EventId")] 
        public virtual Event Event { get; set; } = null!;

        [ForeignKey("UserId")] 
        public virtual User User { get; set; } = null!;
    }
}