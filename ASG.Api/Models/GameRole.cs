using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    /// <summary>
    /// 游戏角色实体 - 用于存储游戏相关的角色信息
    /// </summary>
    public class GameRole
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? GameId { get; set; }

        [MaxLength(50)]
        public string GameRank { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        // 简化的自定义字段 - 直接作为实体属性
        [MaxLength(200)]
        public string? CustomField1 { get; set; }

        [MaxLength(200)]
        public string? CustomField2 { get; set; }

        [MaxLength(200)]
        public string? CustomField3 { get; set; }

        [MaxLength(500)]
        public string? CustomTextArea { get; set; }

        public int? CustomNumber { get; set; }

        public DateTime? CustomDate { get; set; }

        public bool? CustomBoolean { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}