using ASG.Api.Models;

namespace ASG.Api.Repositories
{
    public interface ITeamRepository
    {
        Task<IEnumerable<Team>> GetAllTeamsAsync(int page = 1, int pageSize = 10);
        Task<int> GetTeamCountAsync();
        Task<Team?> GetTeamByIdAsync(Guid id);
        Task<Team?> GetTeamByIdWithPlayersAsync(Guid id);
        Task<Team?> GetTeamByNameAsync(string name);
        Task<Team> CreateTeamAsync(Team team);
        Task<Team> UpdateTeamAsync(Team team);
        Task<bool> DeleteTeamAsync(Guid id);
        Task<bool> VerifyTeamPasswordAsync(Guid teamId, string password);
        Task<bool> TeamExistsAsync(Guid id);
        Task<bool> TeamNameExistsAsync(string name, Guid? excludeId = null);
        Task<IEnumerable<Player>> GetTeamPlayersAsync(Guid teamId);
        Task<int> LikeTeamAsync(Guid id);
    }
}