using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASG.Api.Models
{
    public class TeamReview
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid TeamId { get; set; }

        public Guid? EventId { get; set; }

        [Range(0, 100)]
        public int Rating { get; set; } = 0;

        public string? CommentMarkdown { get; set; }

        public Guid? CommunityPostId { get; set; }

        public string? CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        public virtual Team? Team { get; set; }
        public virtual Event? Event { get; set; }
    }
}
