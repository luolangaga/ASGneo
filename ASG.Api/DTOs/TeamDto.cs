using System.ComponentModel.DataAnnotations;

namespace ASG.Api.DTOs
{
    public class TeamDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? QqNumber { get; set; }
        public int Likes { get; set; } = 0;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? LogoUrl { get; set; }
        public bool HidePlayers { get; set; }
        public List<PlayerDto> Players { get; set; } = new List<PlayerDto>();
        public bool HasDispute { get; set; }
        public string? DisputeDetail { get; set; }
        public Guid? CommunityPostId { get; set; }
        public double RatingAverage { get; set; }
        public int RatingCount { get; set; }
        public List<TeamReviewDto>? Reviews { get; set; }
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

        [Required(ErrorMessage = "QQ号不能为空")]
        [StringLength(20, ErrorMessage = "QQ号不能超过20个字符")]
        public string QqNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "至少需要一个玩家")]
        [MinLength(1, ErrorMessage = "至少需要一个玩家")]
        public List<CreatePlayerDto> Players { get; set; } = new List<CreatePlayerDto>();

        public bool HidePlayers { get; set; }
    }

    public class TeamReviewDto
    {
        public Guid Id { get; set; }
        public Guid TeamId { get; set; }
        public Guid? EventId { get; set; }
        public int Rating { get; set; }
        public string? CommentMarkdown { get; set; }
        public Guid? CommunityPostId { get; set; }
        public string? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateTeamReviewDto
    {
        [Range(0, 100, ErrorMessage = "评分必须在0-100之间")]
        public int Rating { get; set; }
        public string? CommentMarkdown { get; set; }
        public Guid? EventId { get; set; }
        public Guid? CommunityPostId { get; set; }
    }

    public class UpdateTeamDto
    {
        [Required(ErrorMessage = "战队名称不能为空")]
        [StringLength(100, ErrorMessage = "战队名称不能超过100个字符")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "描述不能超过500个字符")]
        public string? Description { get; set; }

        [StringLength(20, ErrorMessage = "QQ号不能超过20个字符")]
        public string? QqNumber { get; set; }

        public bool HidePlayers { get; set; }
        public List<UpdatePlayerDto> Players { get; set; } = new List<UpdatePlayerDto>();
    }

    public class TransferOwnerDto
    {
        [Required]
        public string TargetUserId { get; set; } = string.Empty;
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

    public class TeamInviteDto
    {
        public Guid TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public Guid Token { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class AcceptTeamInviteDto
    {
        public Guid Token { get; set; }
        public CreatePlayerDto? Player { get; set; }
    }
}
