using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASG.Api.Models
{
    public class Team
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(20)]
        public string? QqNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

public int Likes { get; set; } = 0;

// 外键 - 战队拥有者的用户ID
        public string? UserId { get; set; }
        public string? OwnerId { get; set; }

        public Guid? InviteToken { get; set; }
        public DateTime? InviteExpiresAt { get; set; }

        // 导航属性 - 一个战队有多个玩家
        public virtual ICollection<Player> Players { get; set; } = new List<Player>();

        // 导航属性 - 一个战队有一个拥有者（用户）
        public virtual User? Owner { get; set; }

        // 导航属性 - 战队参与的赛事
        public virtual ICollection<TeamEvent> TeamEvents { get; set; } = new List<TeamEvent>();
    }
}