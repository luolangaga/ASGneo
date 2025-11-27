using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    /// <summary>
    /// 操作审计日志
    /// </summary>
    public class OperationLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MaxLength(64)]
        public string Action { get; set; } = string.Empty;

        public string? UserId { get; set; }

        [MaxLength(64)]
        public string? EntityType { get; set; }

        public Guid? EntityId { get; set; }

        /// <summary>
        /// 详细信息（JSON或文本）
        /// </summary>
        public string? Details { get; set; }
    }
}

