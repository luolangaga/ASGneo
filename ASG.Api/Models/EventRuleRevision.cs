using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    /// <summary>
    /// 赛事规则版本记录
    /// </summary>
    public class EventRuleRevision
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid EventId { get; set; }
        public Event? Event { get; set; }

        /// <summary>
        /// 版本号（自增维护）
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// 规则内容（Markdown）
        /// </summary>
        public string ContentMarkdown { get; set; } = string.Empty;

        /// <summary>
        /// 变更说明
        /// </summary>
        [MaxLength(500)]
        public string? ChangeNotes { get; set; }

        public string? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 是否已发布（当前版本设置为赛事规则）
        /// </summary>
        public bool IsPublished { get; set; } = false;
        public DateTime? PublishedAt { get; set; }
    }
}

