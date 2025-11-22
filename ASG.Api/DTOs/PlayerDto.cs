using System.ComponentModel.DataAnnotations;

namespace ASG.Api.DTOs
{
    public class PlayerDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? GameId { get; set; }
        public string? GameRank { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? TeamId { get; set; }
    }

    public class CreatePlayerDto
    {
        [Required(ErrorMessage = "玩家名称不能为空")]
        [StringLength(100, ErrorMessage = "玩家名称不能超过100个字符")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "游戏ID不能超过100个字符")]
        public string? GameId { get; set; }

        [StringLength(50, ErrorMessage = "游戏等级不能超过50个字符")]
        public string? GameRank { get; set; }

        [StringLength(500, ErrorMessage = "描述不能超过500个字符")]
        public string? Description { get; set; }
    }

    public class UpdatePlayerDto
    {
        public Guid? Id { get; set; } // 如果为null，表示新增玩家

        [Required(ErrorMessage = "玩家名称不能为空")]
        [StringLength(100, ErrorMessage = "玩家名称不能超过100个字符")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "游戏ID不能超过100个字符")]
        public string? GameId { get; set; }

        [StringLength(50, ErrorMessage = "游戏等级不能超过50个字符")]
        public string? GameRank { get; set; }

        [StringLength(500, ErrorMessage = "描述不能超过500个字符")]
        public string? Description { get; set; }
    }
}