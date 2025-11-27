namespace ASG.Api.DTOs
{
    public class RecruitmentDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public Guid EventId { get; set; }
        public string PositionType { get; set; } = string.Empty;
        public int PayPerMatch { get; set; }
        public int Slots { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<Guid> MatchIds { get; set; } = new();
    }

    public class CreateRecruitmentDto
    {
        public string Title { get; set; } = string.Empty;
        public Guid EventId { get; set; }
        public string PositionType { get; set; } = string.Empty;
        public int PayPerMatch { get; set; }
        public int Slots { get; set; }
        public string? Description { get; set; }
        public List<Guid> MatchIds { get; set; } = new();
    }

    public class UpdateRecruitmentDto
    {
        public string Title { get; set; } = string.Empty;
        public Guid EventId { get; set; }
        public string PositionType { get; set; } = string.Empty;
        public int PayPerMatch { get; set; }
        public int Slots { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<Guid> MatchIds { get; set; } = new();
    }

    public class RecruitmentApplicationDto
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public Guid EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public DateTime? NextMatchTime { get; set; }
        public string? Venue { get; set; }
        public string ApplicantUserId { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ApplyRecruitmentDto
    {
        public string? Note { get; set; }
    }

    public class SyncMatchesDto
    {
        public List<Guid> MatchIds { get; set; } = new List<Guid>();
    }

    public class PayrollEntryDto
    {
        public string Date { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public string MatchTitle { get; set; } = string.Empty;
        public string PositionType { get; set; } = string.Empty;
        public int PayPerMatch { get; set; }
        public int Amount { get; set; }
        public Guid Id { get; set; }
    }

    public class AssignmentDto
    {
        public Guid MatchId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string MatchTitle { get; set; } = string.Empty;
        public string PositionType { get; set; } = string.Empty;
        public int PayPerMatch { get; set; }
    }
}
