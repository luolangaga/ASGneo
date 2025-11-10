using System.ComponentModel.DataAnnotations;

namespace ASG.Api.Models
{
    /// <summary>
    /// 赛事实体
    /// </summary>
    public class Event
    {
        /// <summary>
        /// 赛事唯一标识符
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// 赛事名称
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 赛事描述
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// 开始报名时间
        /// </summary>
        [Required]
        public DateTime RegistrationStartTime { get; set; }

        /// <summary>
        /// 结束报名时间
        /// </summary>
        [Required]
        public DateTime RegistrationEndTime { get; set; }

        /// <summary>
        /// 比赛开始时间
        /// </summary>
        [Required]
        public DateTime CompetitionStartTime { get; set; }

        /// <summary>
        /// 比赛结束时间
        /// </summary>
        public DateTime? CompetitionEndTime { get; set; }

        /// <summary>
        /// 最大参赛队伍数量
        /// </summary>
        public int? MaxTeams { get; set; }

        /// <summary>
        /// 赛事状态
        /// </summary>
        [Required]
        public EventStatus Status { get; set; } = EventStatus.Draft;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 创建者用户ID
        /// </summary>
        public string? CreatedByUserId { get; set; }

        /// <summary>
        /// 冠军战队ID（可为空，表示尚未设置）
        /// </summary>
        public Guid? ChampionTeamId { get; set; }

        /// <summary>
        /// 冠军战队导航属性
        /// </summary>
        public virtual Team? ChampionTeam { get; set; }

        /// <summary>
        /// 参赛队伍关联
        /// </summary>
        public virtual ICollection<TeamEvent> TeamEvents { get; set; } = new List<TeamEvent>();
        /// <summary>
        /// 赛程关联
        /// </summary>
        public virtual ICollection<Match> Matches { get; set; } = new List<Match>();
    }

    /// <summary>
    /// 赛事状态枚举
    /// </summary>
    public enum EventStatus
    {
        /// <summary>
        /// 草稿状态
        /// </summary>
        Draft = 0,

        /// <summary>
        /// 报名中
        /// </summary>
        RegistrationOpen = 1,

        /// <summary>
        /// 报名已结束
        /// </summary>
        RegistrationClosed = 2,

        /// <summary>
        /// 比赛进行中
        /// </summary>
        InProgress = 3,

        /// <summary>
        /// 比赛已结束
        /// </summary>
        Completed = 4,

        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled = 5
    }
}