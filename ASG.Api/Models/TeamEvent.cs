using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    /// <summary>
    /// 战队赛事关联表（多对多关系）
    /// </summary>
    public class TeamEvent
    {
        /// <summary>
        /// 战队ID
        /// </summary>
        [Required]
        public Guid TeamId { get; set; }

        /// <summary>
        /// 赛事ID
        /// </summary>
        [Required]
        public Guid EventId { get; set; }

        /// <summary>
        /// 报名时间
        /// </summary>
        public DateTime RegistrationTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 报名状态
        /// </summary>
        [Required]
        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

        /// <summary>
        /// 备注信息
        /// </summary>
        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>
        /// 报名用户ID（哪个用户为战队报名的）
        /// </summary>
        public string? RegisteredByUserId { get; set; }

        public Guid? InviteToken { get; set; }
        public DateTime? InviteExpiresAt { get; set; }

        /// <summary>
        /// 战队导航属性
        /// </summary>
        public virtual Team Team { get; set; } = null!;

        /// <summary>
        /// 赛事导航属性
        /// </summary>
        public virtual Event Event { get; set; } = null!;
    }

    /// <summary>
    /// 报名状态枚举
    /// </summary>
    public enum RegistrationStatus
    {
        /// <summary>
        /// 待审核
        /// </summary>
        Pending = 0,

        /// <summary>
        /// 已报名
        /// </summary>
        Registered = 1,

        /// <summary>
        /// 已确认/已批准
        /// </summary>
        Confirmed = 2,

        /// <summary>
        /// 已批准
        /// </summary>
        Approved = 3,

        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled = 4,

        /// <summary>
        /// 被拒绝
        /// </summary>
        Rejected = 5
    }
}