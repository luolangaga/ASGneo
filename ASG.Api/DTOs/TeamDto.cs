using System.ComponentModel.DataAnnotations;

namespace ASG.Api.DTOs
{
    public class TeamDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Likes { get; set; } = 0;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? LogoUrl { get; set; }
        public List<PlayerDto> Players { get; set; } = new List<PlayerDto>();
    }

    public class CreateTeamDto
    {
        [Required(ErrorMessage = "战队名称不能为空")]
        [StringLength(100, ErrorMessage = "战队名称不能超过100个字符")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "密码不能为空")]
        [StringLength(255, MinimumLength = 6, ErrorMessage = "密码长度必须在6-255个字符之间")]
        public string Password { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "描述不能超过500个字符")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "至少需要一个玩家")]
        [MinLength(1, ErrorMessage = "至少需要一个玩家")]
        public List<CreatePlayerDto> Players { get; set; } = new List<CreatePlayerDto>();
    }

    public class UpdateTeamDto
    {
        [Required(ErrorMessage = "战队名称不能为空")]
        [StringLength(100, ErrorMessage = "战队名称不能超过100个字符")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "描述不能超过500个字符")]
        public string? Description { get; set; }

        public List<UpdatePlayerDto> Players { get; set; } = new List<UpdatePlayerDto>();
    }

    public class TeamBindDto
    {
        [Required(ErrorMessage = "战队ID不能为空")]
        public Guid TeamId { get; set; }

        [Required(ErrorMessage = "密码不能为空")]
        public string Password { get; set; } = string.Empty;
    }

    public class TeamBindByNameDto
    {
        [Required(ErrorMessage = "战队名称不能为空")]
        [StringLength(100, ErrorMessage = "战队名称不能超过100个字符")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "密码不能为空")]
        public string Password { get; set; } = string.Empty;
    }

    public class ChangeTeamPasswordDto
    {
        [Required(ErrorMessage = "当前密码不能为空")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "新密码不能为空")]
        [StringLength(255, MinimumLength = 6, ErrorMessage = "新密码长度必须在6-255个字符之间")]
        public string NewPassword { get; set; } = string.Empty;
    }
}