using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASG.Api.Models
{
    public class Player
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? GameId { get; set; }

        [MaxLength(50)]
        public string? GameRank { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 外键 - 属于哪个战队
        public Guid TeamId { get; set; }

        // 外键 - 关联的用户ID（可选）
        public string? UserId { get; set; }

        // 导航属性 - 所属战队
        [ForeignKey("TeamId")]
        public virtual Team Team { get; set; } = null!;
    }
}