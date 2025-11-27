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
        /// 搜索赛事（按名称或描述，分页）
        /// </summary>
        /// <param name="query">搜索关键字</param>
        /// <param name="page">页码（从1开始）</param>
        /// <param name="pageSize">每页数量</param>
        /// <returns>赛事列表（当前页）</returns>
        Task<IEnumerable<Event>> SearchEventsAsync(string query, int page = 1, int pageSize = 12);

        /// <summary>
        /// 搜索赛事的总数（按名称或描述）
        /// </summary>
        /// <param name="query">搜索关键字</param>
        /// <returns>总数</returns>
        Task<int> GetSearchEventsCountAsync(string query);

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
        /// 获取赛事的报名战队
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <returns>报名战队列表</returns>
        Task<IEnumerable<TeamEvent>> GetEventRegistrationsAsync(Guid eventId);

        /// <summary>
        /// 战队报名赛事
        /// </summary>
        /// <param name="teamEvent">战队赛事关联信息</param>
        /// <returns>报名信息</returns>
        Task<TeamEvent> RegisterTeamToEventAsync(TeamEvent teamEvent);

        /// <summary>
        /// 取消战队报名
        /// </summary>
        /// <param name="teamId">战队ID</param>
        /// <param name="eventId">赛事ID</param>
        /// <returns>是否取消成功</returns>
        Task<bool> UnregisterTeamFromEventAsync(Guid teamId, Guid eventId);

        /// <summary>
        /// 更新战队报名状态
        /// </summary>
        /// <param name="teamId">战队ID</param>
        /// <param name="eventId">赛事ID</param>
        /// <param name="status">新状态</param>
        /// <param name="notes">备注</param>
        /// <returns>更新的报名信息</returns>
        Task<TeamEvent?> UpdateTeamRegistrationStatusAsync(Guid teamId, Guid eventId, RegistrationStatus status, string? notes = null);

        /// <summary>
        /// 检查战队是否已报名赛事
        /// </summary>
        /// <param name="teamId">战队ID</param>
        /// <param name="eventId">赛事ID</param>
        /// <returns>是否已报名</returns>
        Task<bool> IsTeamRegisteredAsync(Guid teamId, Guid eventId);

        /// <summary>
        /// 获取战队的报名赛事
        /// </summary>
        /// <param name="teamId">战队ID</param>
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
        /// 分页获取正在报名的赛事
        /// </summary>
        /// <param name="page">页码（从1开始）</param>
        /// <param name="pageSize">每页数量</param>
        /// <returns>正在报名的赛事列表（当前页）</returns>
        Task<IEnumerable<Event>> GetActiveRegistrationEventsAsync(int page, int pageSize);

        /// <summary>
        /// 获取正在报名的赛事总数
        /// </summary>
        /// <returns>总数</returns>
        Task<int> GetActiveRegistrationEventsCountAsync();

        /// <summary>
        /// 获取即将开始的赛事
        /// </summary>
        /// <returns>即将开始的赛事列表</returns>
        Task<IEnumerable<Event>> GetUpcomingEventsAsync();

        /// <summary>
        /// 分页获取即将开始的赛事
        /// </summary>
        /// <param name="page">页码（从1开始）</param>
        /// <param name="pageSize">每页数量</param>
        /// <returns>即将开始的赛事列表（当前页）</returns>
        Task<IEnumerable<Event>> GetUpcomingEventsAsync(int page, int pageSize);

        /// <summary>
        /// 获取即将开始的赛事总数
        /// </summary>
        /// <returns>总数</returns>
        Task<int> GetUpcomingEventsCountAsync();

        /// <summary>
        /// 获取指定战队获得冠军的赛事列表
        /// </summary>
        /// <param name="teamId">战队ID</param>
        /// <returns>该战队作为冠军的赛事列表</returns>
        Task<IEnumerable<Event>> GetChampionEventsByTeamAsync(Guid teamId);

        Task<bool> AddEventAdminAsync(Guid eventId, string userId);
        Task<bool> RemoveEventAdminAsync(Guid eventId, string userId);
        Task<IEnumerable<string>> GetEventAdminUserIdsAsync(Guid eventId);
        Task<bool> IsEventAdminAsync(Guid eventId, string userId);

        // 规则版本化
        Task<EventRuleRevision> CreateRuleRevisionAsync(EventRuleRevision rev);
        Task<IEnumerable<EventRuleRevision>> GetRuleRevisionsAsync(Guid eventId);
        Task<bool> PublishRuleRevisionAsync(Guid eventId, Guid revisionId);

        // 报名表 Schema 存储于 Event.CustomData
        Task<bool> UpdateRegistrationFormSchemaAsync(Guid eventId, string schemaJson);

        // 报名答案（按战队唯一）
        Task<EventRegistrationAnswer> UpsertRegistrationAnswersAsync(Guid eventId, Guid teamId, string answersJson, string? submittedByUserId);
        Task<EventRegistrationAnswer?> GetRegistrationAnswersAsync(Guid eventId, Guid teamId);
    }
}
