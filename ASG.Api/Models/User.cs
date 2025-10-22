using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

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

        public string FullName => $"{FirstName} {LastName}";
        public string RoleDisplayName => Role.GetDisplayName();
        public string RoleName => Role.GetRoleName();
    }
}