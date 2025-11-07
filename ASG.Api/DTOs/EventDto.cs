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
        public DateTimeOffset RegistrationStartTime { get; set; }
        public DateTimeOffset RegistrationEndTime { get; set; }
        public DateTimeOffset CompetitionStartTime { get; set; }
        public DateTimeOffset? CompetitionEndTime { get; set; }
        public int? MaxTeams { get; set; }
        public EventStatus Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? CreatedByUserId { get; set; }
        public int RegisteredTeamsCount { get; set; }
        public List<TeamEventDto>? RegisteredTeams { get; set; }
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

        [Required(ErrorMessage = "报名开始时间不能为空")]
        public DateTimeOffset RegistrationStartTime { get; set; }

        [Required(ErrorMessage = "报名结束时间不能为空")]
        public DateTimeOffset RegistrationEndTime { get; set; }

        [Required(ErrorMessage = "比赛开始时间不能为空")]
        public DateTimeOffset CompetitionStartTime { get; set; }

        public DateTimeOffset? CompetitionEndTime { get; set; }

        [Range(1, 1000, ErrorMessage = "最大参赛队伍数量必须在1-1000之间")]
        public int? MaxTeams { get; set; }
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

        [Required(ErrorMessage = "报名开始时间不能为空")]
        public DateTimeOffset RegistrationStartTime { get; set; }

        [Required(ErrorMessage = "报名结束时间不能为空")]
        public DateTimeOffset RegistrationEndTime { get; set; }

        [Required(ErrorMessage = "比赛开始时间不能为空")]
        public DateTimeOffset CompetitionStartTime { get; set; }

        public DateTimeOffset? CompetitionEndTime { get; set; }

        [Range(1, 1000, ErrorMessage = "最大参赛队伍数量必须在1-1000之间")]
        public int? MaxTeams { get; set; }

        public EventStatus Status { get; set; }
    }

    /// <summary>
    /// 团队赛事关联DTO
    /// </summary>
    public class TeamEventDto
    {
        public Guid TeamId { get; set; }
        public Guid EventId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? EventName { get; set; }
        public DateTimeOffset RegistrationTime { get; set; }
        public RegistrationStatus Status { get; set; }
        public string? Notes { get; set; }
        public string? RegisteredByUserId { get; set; }
    }

    /// <summary>
    /// 团队报名赛事DTO
    /// </summary>
    public class RegisterTeamToEventDto
    {
        [Required(ErrorMessage = "团队ID不能为空")]
        public Guid TeamId { get; set; }

        [StringLength(500, ErrorMessage = "备注长度不能超过500个字符")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// 更新团队报名状态DTO
    /// </summary>
    public class UpdateTeamRegistrationDto
    {
        [Required(ErrorMessage = "报名状态不能为空")]
        public RegistrationStatus Status { get; set; }

        [StringLength(500, ErrorMessage = "备注长度不能超过500个字符")]
        public string? Notes { get; set; }
    }
}