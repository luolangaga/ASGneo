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

        // 团队关系 - 一个用户可以拥有一个团队
        public Guid? TeamId { get; set; }

        // 导航属性 - 拥有的团队
        [ForeignKey("TeamId")]
        public virtual Team? OwnedTeam { get; set; }

        public string FullName => $"{FirstName} {LastName}";
        public string RoleDisplayName => Role.GetDisplayName();
        public string RoleName => Role.GetRoleName();
    }
}