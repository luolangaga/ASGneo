using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASG.Api.Models
{
    public class Match
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HomeTeamId { get; set; }

        [Required]
        public Guid AwayTeamId { get; set; }

        [Required]
        public DateTime MatchTime { get; set; }

        [Required]
        public Guid EventId { get; set; }

        [MaxLength(500)]
        public string? LiveLink { get; set; }

        public string CustomData { get; set; } = "{}"; // JSON string for custom fields

        [MaxLength(100)]
        public string? Commentator { get; set; }

        [MaxLength(100)]
        public string? Director { get; set; }

        [MaxLength(100)]
        public string? Referee { get; set; }

        public int Likes { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("HomeTeamId")]
        public virtual Team HomeTeam { get; set; } = null!;

        [ForeignKey("AwayTeamId")]
        public virtual Team AwayTeam { get; set; } = null!;

        [ForeignKey("EventId")]
        public virtual Event Event { get; set; } = null!;
    }
}