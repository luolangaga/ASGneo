using ASG.Api.DTOs;

namespace ASG.Api.Services
{
    public interface ITeamService
    {
        Task<PagedResult<TeamDto>> GetAllTeamsAsync(int page = 1, int pageSize = 10);
        Task<PagedResult<TeamDto>> SearchTeamsByNameAsync(string name, int page = 1, int pageSize = 10);
        Task<TeamDto?> GetTeamByIdAsync(Guid id, string? userId = null);
        Task<TeamDto> CreateTeamAsync(CreateTeamDto createTeamDto, string? userId = null);
        Task<TeamDto> UpdateTeamAsync(Guid id, UpdateTeamDto updateTeamDto, string? userId = null);
        Task<bool> DeleteTeamAsync(Guid id, string? userId = null, bool isAdmin = false);
        Task<bool> BindTeamAsync(Guid teamId, string password, string userId);
        Task<bool> BindTeamByNameAsync(string name, string password, string userId);
        Task<bool> UnbindTeamAsync(string userId);
        Task<bool> ChangeTeamPasswordAsync(Guid teamId, ChangeTeamPasswordDto changePasswordDto, string userId);
        Task<bool> VerifyTeamOwnershipAsync(Guid teamId, string userId);
        Task<bool> TeamExistsAsync(Guid id);
        Task<int> LikeTeamAsync(Guid id);
        Task<TeamInviteDto> GenerateTeamInviteAsync(Guid teamId, string userId, int validDays = 7);
        Task<TeamInviteDto?> GetTeamInviteAsync(Guid token);
        Task<PlayerDto> AcceptTeamInviteAsync(Guid token, string userId, CreatePlayerDto playerDto);
        Task<bool> LeaveTeamAsync(Guid teamId, string userId);
        Task<PlayerDto?> GetMyPlayerAsync(string userId);
        Task<PlayerDto> UpsertMyPlayerAsync(string userId, CreatePlayerDto playerDto);
        Task<bool> TransferTeamOwnershipAsync(Guid teamId, string currentUserId, string targetUserId);
        Task<IEnumerable<TeamReviewDto>> GetTeamReviewsAsync(Guid teamId);
        Task<TeamReviewDto> AddTeamReviewAsync(Guid teamId, string userId, CreateTeamReviewDto dto);
        Task<bool> SetTeamDisputeAsync(Guid teamId, bool hasDispute, string? disputeDetail = null, Guid? communityPostId = null);
    }
}
