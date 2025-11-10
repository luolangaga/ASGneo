using ASG.Api.DTOs;

namespace ASG.Api.Services
{
    public interface ITeamService
    {
        Task<PagedResult<TeamDto>> GetAllTeamsAsync(int page = 1, int pageSize = 10);
        Task<PagedResult<TeamDto>> SearchTeamsByNameAsync(string name, int page = 1, int pageSize = 10);
        Task<TeamDto?> GetTeamByIdAsync(Guid id);
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
    }
}