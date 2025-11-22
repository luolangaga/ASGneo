using System.ComponentModel.DataAnnotations;
using ASG.Api.Models;

namespace ASG.Api.DTOs
{
    public class UserRegistrationDto
    {
        [Required(ErrorMessage = "邮箱是必填项")]
        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "密码是必填项")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度必须在6-100个字符之间")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "姓名是必填项")]
        [StringLength(100, ErrorMessage = "姓名不能超过100个字符")]
        public string FullName { get; set; } = string.Empty;

        public UserRole Role { get; set; } = UserRole.User;
    }

    public class UserLoginDto
    {
        [Required(ErrorMessage = "邮箱是必填项")]
        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "密码是必填项")]
        public string Password { get; set; } = string.Empty;
    }

    public class UserResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string RoleDisplayName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }
        public string? AvatarUrl { get; set; }
        public Guid? TeamId { get; set; }
        public int EmailCredits { get; set; }
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expires { get; set; }
        public UserResponseDto User { get; set; } = new();
    }

    public class UpdateProfileDto
    {
        [Required(ErrorMessage = "姓名是必填项")]
        [StringLength(100, ErrorMessage = "姓名不能超过100个字符")]
        public string FullName { get; set; } = string.Empty;
    }

    public class UpdateUserRoleDto
    {
        [Required(ErrorMessage = "用户ID是必填项")]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "角色是必填项")]
        public UserRole Role { get; set; }
    }

    public class RoleInfoDto
    {
        public UserRole Role { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public class UserListDto
    {
        public List<UserResponseDto> Users { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class UpdateEmailCreditsDto
    {
        [Range(0, int.MaxValue, ErrorMessage = "积分必须为非负整数")]
        public int Credits { get; set; }
    }

    public class PasswordResetRequestDto
    {
        [Required(ErrorMessage = "邮箱是必填项")]
        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        public string Email { get; set; } = string.Empty;
    }

    public class PasswordResetConfirmDto
    {
        [Required(ErrorMessage = "邮箱是必填项")]
        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "令牌是必填项")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "新密码是必填项")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度必须在6-100个字符之间")]
        public string NewPassword { get; set; } = string.Empty;
    }
}