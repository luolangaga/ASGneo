namespace ASG.Api.DTOs
{
    public class MatchDto
    {
        public Guid Id { get; set; }
        public Guid HomeTeamId { get; set; }
        public string HomeTeamName { get; set; } = string.Empty;
        public Guid AwayTeamId { get; set; }
        public string AwayTeamName { get; set; } = string.Empty;
        public DateTimeOffset MatchTime { get; set; }
        public Guid EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string? LiveLink { get; set; }
        public string CustomData { get; set; } = "{}";
        public string? Commentator { get; set; }
        public string? Director { get; set; }
        public string? Referee { get; set; }
        public int Likes { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CreateMatchDto
    {
        public Guid HomeTeamId { get; set; }
        public Guid AwayTeamId { get; set; }
        public DateTimeOffset MatchTime { get; set; }
        public Guid EventId { get; set; }
        public string? LiveLink { get; set; }
        public string CustomData { get; set; } = "{}";
        public string? Commentator { get; set; }
        public string? Director { get; set; }
        public string? Referee { get; set; }
    }

    public class UpdateMatchDto
    {
        public DateTimeOffset? MatchTime { get; set; }
        public string? LiveLink { get; set; }
        public string? CustomData { get; set; }
        public string? Commentator { get; set; }
        public string? Director { get; set; }
        public string? Referee { get; set; }
    }
}