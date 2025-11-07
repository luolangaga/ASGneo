using ASG.Api.Models;

namespace ASG.Api.Repositories
{
    /// <summary>
    /// 赛事数据访问接口
    /// </summary>
    public interface IEventRepository
    {
        /// <summary>
        /// 获取所有赛事
        /// </summary>
        /// <returns>赛事列表</returns>
        Task<IEnumerable<Event>> GetAllEventsAsync();

        /// <summary>
        /// 根据ID获取赛事
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <returns>赛事信息</returns>
        Task<Event?> GetEventByIdAsync(Guid id);

        /// <summary>
        /// 根据名称获取赛事
        /// </summary>
        /// <param name="name">赛事名称</param>
        /// <returns>赛事信息</returns>
        Task<Event?> GetEventByNameAsync(string name);

        /// <summary>
        /// 创建赛事
        /// </summary>
        /// <param name="eventEntity">赛事信息</param>
        /// <returns>创建的赛事</returns>
        Task<Event> CreateEventAsync(Event eventEntity);

        /// <summary>
        /// 更新赛事
        /// </summary>
        /// <param name="eventEntity">赛事信息</param>
        /// <returns>更新的赛事</returns>
        Task<Event> UpdateEventAsync(Event eventEntity);

        /// <summary>
        /// 删除赛事
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <returns>是否删除成功</returns>
        Task<bool> DeleteEventAsync(Guid id);

        /// <summary>
        /// 获取赛事的报名团队
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <returns>报名团队列表</returns>
        Task<IEnumerable<TeamEvent>> GetEventRegistrationsAsync(Guid eventId);

        /// <summary>
        /// 团队报名赛事
        /// </summary>
        /// <param name="teamEvent">团队赛事关联信息</param>
        /// <returns>报名信息</returns>
        Task<TeamEvent> RegisterTeamToEventAsync(TeamEvent teamEvent);

        /// <summary>
        /// 取消团队报名
        /// </summary>
        /// <param name="teamId">团队ID</param>
        /// <param name="eventId">赛事ID</param>
        /// <returns>是否取消成功</returns>
        Task<bool> UnregisterTeamFromEventAsync(Guid teamId, Guid eventId);

        /// <summary>
        /// 更新团队报名状态
        /// </summary>
        /// <param name="teamId">团队ID</param>
        /// <param name="eventId">赛事ID</param>
        /// <param name="status">新状态</param>
        /// <param name="notes">备注</param>
        /// <returns>更新的报名信息</returns>
        Task<TeamEvent?> UpdateTeamRegistrationStatusAsync(Guid teamId, Guid eventId, RegistrationStatus status, string? notes = null);

        /// <summary>
        /// 检查团队是否已报名赛事
        /// </summary>
        /// <param name="teamId">团队ID</param>
        /// <param name="eventId">赛事ID</param>
        /// <returns>是否已报名</returns>
        Task<bool> IsTeamRegisteredAsync(Guid teamId, Guid eventId);

        /// <summary>
        /// 获取团队的报名赛事
        /// </summary>
        /// <param name="teamId">团队ID</param>
        /// <returns>报名赛事列表</returns>
        Task<IEnumerable<TeamEvent>> GetTeamRegistrationsAsync(Guid teamId);

        /// <summary>
        /// 获取用户创建的赛事
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>赛事列表</returns>
        Task<IEnumerable<Event>> GetEventsByCreatorAsync(string userId);

        /// <summary>
        /// 获取正在报名的赛事
        /// </summary>
        /// <returns>正在报名的赛事列表</returns>
        Task<IEnumerable<Event>> GetActiveRegistrationEventsAsync();

        /// <summary>
        /// 获取即将开始的赛事
        /// </summary>
        /// <returns>即将开始的赛事列表</returns>
        Task<IEnumerable<Event>> GetUpcomingEventsAsync();
    }
}