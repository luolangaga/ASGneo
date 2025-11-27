using ASG.Api.DTOs;
using ASG.Api.Models;

namespace ASG.Api.Services
{
    /// <summary>
    /// 赛程业务逻辑接口
    /// </summary>
    public interface IMatchService
    {
        /// <summary>
        /// 获取所有赛程（带分页和按赛事过滤）
        /// </summary>
        /// <param name="eventId">赛事ID（可选）</param>
        /// <param name="page">页码</param>
        /// <param name="pageSize">每页大小</param>
        /// <returns>赛程DTO列表</returns>
        Task<IEnumerable<MatchDto>> GetAllMatchesAsync(Guid? eventId = null, int page = 1, int pageSize = 10, int? groupIndex = null, string? groupLabel = null);

        /// <summary>
        /// 获取赛程总数
        /// </summary>
        /// <param name="eventId">赛事ID（可选）</param>
        /// <returns>总数</returns>
        Task<int> GetMatchCountAsync(Guid? eventId = null, int? groupIndex = null, string? groupLabel = null);

        /// <summary>
        /// 根据ID获取赛程
        /// </summary>
        /// <param name="id">赛程ID</param>
        /// <returns>赛程DTO</returns>
        Task<MatchDto?> GetMatchByIdAsync(Guid id);

        /// <summary>
        /// 创建赛程
        /// </summary>
        /// <param name="createDto">创建DTO</param>
        /// <param name="userId">操作用户ID</param>
        /// <returns>创建的赛程DTO</returns>
        Task<MatchDto> CreateMatchAsync(CreateMatchDto createDto, string userId);

        /// <summary>
        /// 更新赛程
        /// </summary>
        /// <param name="id">赛程ID</param>
        /// <param name="updateDto">更新DTO</param>
        /// <param name="userId">操作用户ID</param>
        /// <returns>更新的赛程DTO</returns>
        Task<MatchDto?> UpdateMatchAsync(Guid id, UpdateMatchDto updateDto, string userId);

        Task<MatchDto?> UpdateMatchScoresAsync(Guid id, UpdateMatchScoresDto scoresDto, string userId);

        /// <summary>
        /// 删除赛程
        /// </summary>
        /// <param name="id">赛程ID</param>
        /// <param name="userId">操作用户ID</param>
        /// <returns>是否删除成功</returns>
        Task<bool> DeleteMatchAsync(Guid id, string userId);

        /// <summary>
        /// 给赛程点赞
        /// </summary>
        /// <param name="id">赛程ID</param>
        /// <returns>更新后的点赞数</returns>
        Task<int> LikeMatchAsync(Guid id);

        Task<IEnumerable<MatchDto>> GenerateScheduleAsync(Guid eventId, GenerateScheduleRequestDto dto, string userId);
        Task<IEnumerable<ConflictDto>> GetScheduleConflictsAsync(Guid eventId);
        Task<IEnumerable<MatchDto>> GenerateNextRoundAsync(Guid eventId, GenerateNextRoundRequestDto dto, string userId);
    }
}
