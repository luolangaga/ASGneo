using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    public class RecruitmentTaskMatch
    {
        [Required]
        public Guid TaskId { get; set; }
        [Required]
        public Guid MatchId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
