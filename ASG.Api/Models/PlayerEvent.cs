using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    public class PlayerEvent
    {
        [Required]
        public Guid PlayerId { get; set; }

        [Required]
        public Guid EventId { get; set; }

        public DateTime RegistrationTime { get; set; } = DateTime.UtcNow;

        [Required]
        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public string? RegisteredByUserId { get; set; }

        public virtual Player Player { get; set; } = null!;
        public virtual Event Event { get; set; } = null!;
    }
}
