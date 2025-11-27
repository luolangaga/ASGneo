using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASG.Api.Models
{
    public class User : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public UserRole Role { get; set; } = UserRole.User;

        // 邮件积分：用于邮件通知扣费
        public int EmailCredits { get; set; } = 0;

        // 战队关系 - 一个用户可以拥有一个战队
        public Guid? TeamId { get; set; }

        // 用户当前展示的所属战队（成员身份，不等同于拥有者）
        public Guid? DisplayTeamId { get; set; }

        // 导航属性 - 拥有的战队
        [ForeignKey("TeamId")]
        public virtual Team? OwnedTeam { get; set; }

        public string FullName => string.Join(" ", new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        public string RoleDisplayName => Role.GetDisplayName();
        public string RoleName => Role.GetRoleName();
    }
}
