using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    public enum ApplicationStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    public class RecruitmentApplication
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid TaskId { get; set; }

        [Required]
        public string ApplicantUserId { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Note { get; set; }

        [Required]
        public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
