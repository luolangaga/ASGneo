namespace ASG.Api.DTOs
{
    public class MatchDto
    {
        public Guid Id { get; set; }
        public Guid HomeTeamId { get; set; }
        public string HomeTeamName { get; set; } = string.Empty;
        public Guid AwayTeamId { get; set; }
        public string AwayTeamName { get; set; } = string.Empty;
        public DateTime MatchTime { get; set; }
        public Guid EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string? LiveLink { get; set; }
        public string? Stage { get; set; }
        public Guid? WinnerTeamId { get; set; }
        public string? WinnerTeamName { get; set; }
        public string? ReplayLink { get; set; }
        public string CustomData { get; set; } = "{}";
        public string? Commentator { get; set; }
        public string? Director { get; set; }
        public string? Referee { get; set; }
        public int Likes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsToday { get; set; }
    }

    public class CreateMatchDto
    {
        public Guid HomeTeamId { get; set; }
        public Guid AwayTeamId { get; set; }
        public DateTime MatchTime { get; set; }
        public Guid EventId { get; set; }
        public string? LiveLink { get; set; }
        public string CustomData { get; set; } = "{}";
        public string? Commentator { get; set; }
        public string? Director { get; set; }
        public string? Referee { get; set; }
    }

    public class UpdateMatchDto
    {
        public DateTime? MatchTime { get; set; }
        public string? LiveLink { get; set; }
        public string? CustomData { get; set; }
        public string? Commentator { get; set; }
        public string? Director { get; set; }
        public string? Referee { get; set; }
    }

    public class GameScoreDto
    {
        public int Home { get; set; }
        public int Away { get; set; }
    }

    public class UpdateMatchScoresDto
    {
        public int? BestOf { get; set; }
        public List<GameScoreDto> Games { get; set; } = new List<GameScoreDto>();
    }
}
