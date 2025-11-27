using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    public enum PositionType
    {
        Commentator = 0,
        Director = 1,
        Referee = 2
    }

    public enum RecruitmentStatus
    {
        Active = 0,
        Closed = 1
    }

    public class RecruitmentTask
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public Guid EventId { get; set; }

        [Required]
        public PositionType PositionType { get; set; }

        [Required]
        public int PayPerMatch { get; set; }

        [Required]
        public int Slots { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public RecruitmentStatus Status { get; set; } = RecruitmentStatus.Active;

        public string? CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
