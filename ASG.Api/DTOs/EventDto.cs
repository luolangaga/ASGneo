using ASG.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace ASG.Api.DTOs
{
    /// <summary>
    /// 赛事展示DTO
    /// </summary>
    public class EventDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? QqGroup { get; set; }
        public string? RulesMarkdown { get; set; }
        public DateTime RegistrationStartTime { get; set; }
        public DateTime RegistrationEndTime { get; set; }
        public DateTime CompetitionStartTime { get; set; }
        public DateTime? CompetitionEndTime { get; set; }
        public int? MaxTeams { get; set; }
        public EventStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? CreatedByUserId { get; set; }
        public string? LogoUrl { get; set; }
        public Guid? ChampionTeamId { get; set; }
        public string? ChampionTeamName { get; set; }
        public int RegisteredTeamsCount { get; set; }
        public List<TeamEventDto>? RegisteredTeams { get; set; }
        public List<string>? AdminUserIds { get; set; }
        public string? CustomData { get; set; }
        public RegistrationMode RegistrationMode { get; set; }
    }

    /// <summary>
    /// 创建赛事DTO
    /// </summary>
    public class CreateEventDto
    {
        [Required(ErrorMessage = "赛事名称不能为空")]
        [StringLength(100, ErrorMessage = "赛事名称长度不能超过100个字符")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "赛事描述长度不能超过1000个字符")]
        public string? Description { get; set; }

        [StringLength(50, ErrorMessage = "QQ群号长度不能超过50个字符")]
        public string? QqGroup { get; set; }

        public string? RulesMarkdown { get; set; }

        [Required(ErrorMessage = "报名开始时间不能为空")]
        public DateTime RegistrationStartTime { get; set; }

        [Required(ErrorMessage = "报名结束时间不能为空")]
        public DateTime RegistrationEndTime { get; set; }

        [Required(ErrorMessage = "比赛开始时间不能为空")]
        public DateTime CompetitionStartTime { get; set; }

        public DateTime? CompetitionEndTime { get; set; }

        [Range(1, 1000, ErrorMessage = "最大参赛队伍数量必须在1-1000之间")]
        public int? MaxTeams { get; set; }
        public RegistrationMode RegistrationMode { get; set; } = RegistrationMode.Team;
    }

    /// <summary>
    /// 更新赛事DTO
    /// </summary>
    public class UpdateEventDto
    {
        [Required(ErrorMessage = "赛事名称不能为空")]
        [StringLength(100, ErrorMessage = "赛事名称长度不能超过100个字符")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "赛事描述长度不能超过1000个字符")]
        public string? Description { get; set; }

        [StringLength(50, ErrorMessage = "QQ群号长度不能超过50个字符")]
        public string? QqGroup { get; set; }

        public string? RulesMarkdown { get; set; }

        [Required(ErrorMessage = "报名开始时间不能为空")]
        public DateTime RegistrationStartTime { get; set; }

        [Required(ErrorMessage = "报名结束时间不能为空")]
        public DateTime RegistrationEndTime { get; set; }

        [Required(ErrorMessage = "比赛开始时间不能为空")]
        public DateTime CompetitionStartTime { get; set; }

        public DateTime? CompetitionEndTime { get; set; }

        [Range(1, 1000, ErrorMessage = "最大参赛队伍数量必须在1-1000之间")]
        public int? MaxTeams { get; set; }

        public EventStatus Status { get; set; }
        public RegistrationMode RegistrationMode { get; set; } = RegistrationMode.Team;
    }

    /// <summary>
    /// 战队赛事关联DTO
    /// </summary>
    public class TeamEventDto
    {
        public Guid TeamId { get; set; }
        public Guid EventId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? EventName { get; set; }
        public DateTime RegistrationTime { get; set; }
        public RegistrationStatus Status { get; set; }
        public string? Notes { get; set; }
        public string? RegisteredByUserId { get; set; }
        public string? QqNumberMasked { get; set; }
        public string? QqNumberFull { get; set; }
        public bool TeamHasDispute { get; set; }
        public string? TeamDisputeDetail { get; set; }
        public Guid? TeamCommunityPostId { get; set; }
        public double TeamRatingAverage { get; set; }
        public int TeamRatingCount { get; set; }
    }

    public class PlayerEventDto
    {
        public Guid PlayerId { get; set; }
        public Guid EventId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public DateTime RegistrationTime { get; set; }
        public RegistrationStatus Status { get; set; }
        public string? Notes { get; set; }
        public string? RegisteredByUserId { get; set; }
    }

    public class RegisterPlayerToEventDto
    {
        public Guid? PlayerId { get; set; }
        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class CreateSoloTempTeamDto
    {
        [StringLength(100)]
        public string? Name { get; set; }
        [Required]
        public List<Guid> PlayerIds { get; set; } = new List<Guid>();
        public bool ApproveRegistration { get; set; } = true;
    }

    /// <summary>
    /// 战队报名赛事DTO
    /// </summary>
    public class RegisterTeamToEventDto
    {
        [Required(ErrorMessage = "战队ID不能为空")]
        public Guid TeamId { get; set; }

        [StringLength(500, ErrorMessage = "备注长度不能超过500个字符")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// 更新战队报名状态DTO
    /// </summary>
    public class UpdateTeamRegistrationDto
    {
        [Required(ErrorMessage = "报名状态不能为空")]
        public RegistrationStatus Status { get; set; }

        [StringLength(500, ErrorMessage = "备注长度不能超过500个字符")]
        public string? Notes { get; set; }

        /// <summary>
        /// 是否通过邮件通知战队拥有者（将扣除其邮件积分）
        /// </summary>
        public bool NotifyByEmail { get; set; } = false;
    }

    /// <summary>
    /// 设置赛事冠军DTO（传null清除冠军）
    /// </summary>
    public class SetChampionDto
    {
        public Guid? TeamId { get; set; }
    }

    public class AddEventAdminDto
    {
        public string UserId { get; set; } = string.Empty;
    }

    // 规则版本化
    public class CreateRuleRevisionDto
    {
        [Required]
        public string ContentMarkdown { get; set; } = string.Empty;
        [StringLength(500)]
        public string? ChangeNotes { get; set; }
    }

    public class RuleRevisionDto
    {
        public Guid Id { get; set; }
        public Guid EventId { get; set; }
        public int Version { get; set; }
        public string ContentMarkdown { get; set; } = string.Empty;
        public string? ChangeNotes { get; set; }
        public string? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsPublished { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    // 报名表 Schema 与答案
    public class UpdateRegistrationFormSchemaDto
    {
        [Required]
        public string SchemaJson { get; set; } = "{}";
    }

    public class SubmitRegistrationAnswersDto
    {
        [Required]
        public Guid TeamId { get; set; }
        [Required]
        public string AnswersJson { get; set; } = "{}";
    }

    public class UpdateTournamentConfigDto
    {
        [Required]
        public string Format { get; set; } = "single_elim";
        public int? Rounds { get; set; }
        public int? BestOf { get; set; }
        public int? GroupSize { get; set; }
        public int? AdvancePerGroup { get; set; }
        public List<List<Guid>>? Groups { get; set; }
        public List<Guid>? Seeding { get; set; }
        public DateTime? StartTime { get; set; }
        public int? IntervalMinutes { get; set; }
        public DateTime? StartDate { get; set; }
        public string? DailyStartTime { get; set; }
        public int? MaxMatchesPerDay { get; set; }
    }

    public class GenerateScheduleRequestDto
    {
        [Required]
        public string Stage { get; set; } = "single_elim";
        public int? Round { get; set; }
        public int? BestOf { get; set; }
        public DateTime? StartTime { get; set; }
        public int? IntervalMinutes { get; set; }
        public DateTime? StartDate { get; set; }
        public string? DailyStartTime { get; set; }
        public int? MaxMatchesPerDay { get; set; }
    }

    public class GenerateNextRoundRequestDto
    {
        [Required]
        public string Stage { get; set; } = "single_elim";
        public int? BestOf { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? StartDate { get; set; }
        public string? DailyStartTime { get; set; }
        public int? IntervalMinutes { get; set; }
        public int? MaxMatchesPerDay { get; set; }
    }

    public class GenerateTestRegistrationsRequestDto
    {
        public int Count { get; set; } = 64;
        public string? NamePrefix { get; set; }
        public bool Approve { get; set; } = true;
    }

    public class ConflictDto
    {
        public Guid TeamId { get; set; }
        public List<Guid> MatchIds { get; set; } = new List<Guid>();
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}
