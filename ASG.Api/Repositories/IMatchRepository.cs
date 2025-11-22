using ASG.Api.Models;

namespace ASG.Api.Repositories
{
    /// <summary>
    /// 赛程数据访问接口
    /// </summary>
    public interface IMatchRepository
    {
        /// <summary>
        /// 获取所有赛程（带分页和按赛事过滤）
        /// </summary>
        /// <param name="eventId">赛事ID（可选）</param>
        /// <param name="page">页码</param>
        /// <param name="pageSize">每页大小</param>
        /// <returns>赛程列表</returns>
        Task<IEnumerable<Match>> GetAllMatchesAsync(Guid? eventId = null, int page = 1, int pageSize = 10);

        /// <summary>
        /// 获取赛程总数（用于分页）
        /// </summary>
        /// <param name="eventId">赛事ID（可选）</param>
        /// <returns>总数</returns>
        Task<int> GetMatchCountAsync(Guid? eventId = null);

        /// <summary>
        /// 根据ID获取赛程
        /// </summary>
        /// <param name="id">赛程ID</param>
        /// <returns>赛程信息</returns>
        Task<Match?> GetMatchByIdAsync(Guid id);

        /// <summary>
        /// 创建赛程
        /// </summary>
        /// <param name="match">赛程信息</param>
        /// <returns>创建的赛程</returns>
        Task<Match> CreateMatchAsync(Match match);

        /// <summary>
        /// 更新赛程
        /// </summary>
        /// <param name="match">赛程信息</param>
        /// <returns>更新的赛程</returns>
        Task<Match> UpdateMatchAsync(Match match);

        /// <summary>
        /// 批量更新赛程
        /// </summary>
        Task<int> UpdateMatchesAsync(IEnumerable<Match> matches);

        /// <summary>
        /// 按ID集合获取赛程（限定赛事）
        /// </summary>
        Task<IEnumerable<Match>> GetMatchesByIdsAsync(Guid eventId, IEnumerable<Guid> ids);

        /// <summary>
        /// 删除赛程
        /// </summary>
        /// <param name="id">赛程ID</param>
        /// <returns>是否删除成功</returns>
        Task<bool> DeleteMatchAsync(Guid id);

        /// <summary>
        /// 给赛程点赞
        /// </summary>
        /// <param name="id">赛程ID</param>
        /// <returns>更新后的点赞数</returns>
        Task<int> LikeMatchAsync(Guid id);
    }
}