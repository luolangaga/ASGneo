using ASG.Api.DTOs;
using ASG.Api.Models;

namespace ASG.Api.Services
{
    /// <summary>
    /// 赛事业务逻辑接口
    /// </summary>
    public interface IEventService
    {
        /// <summary>
        /// 获取所有赛事
        /// </summary>
        /// <returns>赛事DTO列表</returns>
        Task<IEnumerable<EventDto>> GetAllEventsAsync();

        /// <summary>
        /// 根据ID获取赛事
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <returns>赛事DTO</returns>
        Task<EventDto?> GetEventByIdAsync(Guid id);

        /// <summary>
        /// 创建赛事
        /// </summary>
        /// <param name="createEventDto">创建赛事DTO</param>
        /// <param name="creatorUserId">创建者用户ID</param>
        /// <returns>创建的赛事DTO</returns>
        Task<EventDto> CreateEventAsync(CreateEventDto createEventDto, string creatorUserId);

        /// <summary>
        /// 更新赛事
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <param name="updateEventDto">更新赛事DTO</param>
        /// <param name="userId">操作用户ID</param>
        /// <returns>更新的赛事DTO</returns>
        Task<EventDto?> UpdateEventAsync(Guid id, UpdateEventDto updateEventDto, string userId);

        /// <summary>
        /// 删除赛事
        /// </summary>
        /// <param name="id">赛事ID</param>
        /// <param name="userId">操作用户ID</param>
        /// <returns>是否删除成功</returns>
        Task<bool> DeleteEventAsync(Guid id, string userId);

        /// <summary>
        /// 战队报名赛事
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <param name="registerDto">报名DTO</param>
        /// <param name="userId">操作用户ID</param>
        /// <returns>报名信息DTO</returns>
        Task<TeamEventDto?> RegisterTeamToEventAsync(Guid eventId, RegisterTeamToEventDto registerDto, string userId);

        /// <summary>
        /// 取消战队报名
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <param name="teamId">战队ID</param>
        /// <param name="userId">操作用户ID</param>
        /// <returns>是否取消成功</returns>
        Task<bool> UnregisterTeamFromEventAsync(Guid eventId, Guid teamId, string userId);

        /// <summary>
        /// 更新战队报名状态
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <param name="teamId">战队ID</param>
        /// <param name="updateDto">更新DTO</param>
        /// <param name="userId">操作用户ID</param>
        /// <returns>更新的报名信息DTO</returns>
        Task<TeamEventDto?> UpdateTeamRegistrationStatusAsync(Guid eventId, Guid teamId, UpdateTeamRegistrationDto updateDto, string userId);

        /// <summary>
        /// 获取赛事的报名战队
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <returns>报名战队DTO列表</returns>
        Task<IEnumerable<TeamEventDto>> GetEventRegistrationsAsync(Guid eventId);

        /// <summary>
        /// 获取赛事报名列表，并根据用户权限返回QQ遮蔽或完整号码
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <param name="userId">当前用户ID（可空，未登录视为无权限）</param>
        /// <returns>报名战队DTO列表（含QQ字段）</returns>
        Task<IEnumerable<TeamEventDto>> GetEventRegistrationsWithSensitiveAsync(Guid eventId, string? userId);

        /// <summary>
        /// 获取战队的报名赛事
        /// </summary>
        /// <param name="teamId">战队ID</param>
        /// <returns>报名赛事DTO列表</returns>
        Task<IEnumerable<TeamEventDto>> GetTeamRegistrationsAsync(Guid teamId);

        /// <summary>
        /// 获取用户创建的赛事
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>赛事DTO列表</returns>
        Task<IEnumerable<EventDto>> GetEventsByCreatorAsync(string userId);

        /// <summary>
        /// 获取正在报名的赛事
        /// </summary>
        /// <returns>正在报名的赛事DTO列表</returns>
        Task<IEnumerable<EventDto>> GetActiveRegistrationEventsAsync();

        /// <summary>
        /// 分页获取正在报名的赛事
        /// </summary>
        /// <param name="page">页码（默认1）</param>
        /// <param name="pageSize">每页数量（默认12）</param>
        /// <returns>分页结果</returns>
        Task<PagedResult<EventDto>> GetActiveRegistrationEventsAsync(int page = 1, int pageSize = 12);

        /// <summary>
        /// 获取即将开始的赛事
        /// </summary>
        /// <returns>即将开始的赛事DTO列表</returns>
        Task<IEnumerable<EventDto>> GetUpcomingEventsAsync();

        /// <summary>
        /// 分页获取即将开始的赛事
        /// </summary>
        /// <param name="page">页码（默认1）</param>
        /// <param name="pageSize">每页数量（默认12）</param>
        /// <returns>分页结果</returns>
        Task<PagedResult<EventDto>> GetUpcomingEventsAsync(int page = 1, int pageSize = 12);

        /// <summary>
        /// 搜索赛事（按名称或描述，分页）
        /// </summary>
        /// <param name="query">搜索关键字</param>
        /// <param name="page">页码（默认1）</param>
        /// <param name="pageSize">每页数量（默认12）</param>
        /// <returns>分页结果</returns>
        Task<PagedResult<EventDto>> SearchEventsAsync(string query, int page = 1, int pageSize = 12);

        /// <summary>
        /// 获取指定战队获得冠军的赛事（战队荣誉）
        /// </summary>
        /// <param name="teamId">战队ID</param>
        /// <returns>赛事DTO列表</returns>
        Task<IEnumerable<EventDto>> GetChampionEventsByTeamAsync(Guid teamId);

        /// <summary>
        /// 验证用户是否有权限操作赛事
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <param name="userId">用户ID</param>
        /// <returns>是否有权限</returns>
        Task<bool> CanUserManageEventAsync(Guid eventId, string userId);

        /// <summary>
        /// 验证用户是否有权限操作战队报名
        /// </summary>
        /// <param name="teamId">战队ID</param>
        /// <param name="userId">用户ID</param>
        /// <returns>是否有权限</returns>
        Task<bool> CanUserManageTeamRegistrationAsync(Guid teamId, string userId);

        /// <summary>
        /// 设置或清除赛事冠军战队（需要管理权限）
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <param name="dto">设置冠军DTO</param>
        /// <param name="userId">操作用户ID</param>
        /// <returns>更新后的赛事DTO</returns>
        Task<EventDto?> SetChampionTeamAsync(Guid eventId, SetChampionDto dto, string userId);

        /// <summary>
        /// 导出赛事报名信息为CSV（包含队员信息），需要管理权限
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <param name="userId">操作用户ID</param>
        /// <returns>CSV字节数组（UTF-8 BOM）</returns>
        Task<byte[]> ExportEventRegistrationsCsvAsync(Guid eventId, string userId);

        Task<IEnumerable<UserResponseDto>> GetEventAdminsAsync(Guid eventId);
        Task<bool> AddEventAdminAsync(Guid eventId, string userId, string targetUserId);
        Task<bool> RemoveEventAdminAsync(Guid eventId, string userId, string targetUserId);
        Task<bool> IsEventAdminAsync(Guid eventId, string userId);

        // 赛程图画布持久化
        Task<bool> SaveBracketCanvasAsync(Guid eventId, string userId, string canvasJson);
        Task<string> GetBracketCanvasAsync(Guid eventId);

        Task<bool> CanUserManageAnyEventOfTeamAsync(Guid teamId, string userId);

        // 规则版本化
        Task<RuleRevisionDto> CreateRuleRevisionAsync(Guid eventId, CreateRuleRevisionDto dto, string userId);
        Task<IEnumerable<RuleRevisionDto>> GetRuleRevisionsAsync(Guid eventId);
        Task<bool> PublishRuleRevisionAsync(Guid eventId, Guid revisionId, string userId);

        // 报名表 Schema 存储于 Event.CustomData
        Task<bool> UpdateRegistrationFormSchemaAsync(Guid eventId, UpdateRegistrationFormSchemaDto dto, string userId);

        // 报名答案（按战队唯一）
        Task<bool> SubmitRegistrationAnswersAsync(Guid eventId, SubmitRegistrationAnswersDto dto, string userId);

        // 读取报名表 Schema
        Task<string> GetRegistrationFormSchemaAsync(Guid eventId);

        // 读取报名答案 JSON
        Task<string> GetRegistrationAnswersAsync(Guid eventId, Guid teamId, string userId);

        Task<bool> UpdateTournamentConfigAsync(Guid eventId, UpdateTournamentConfigDto dto, string userId);

        Task<IEnumerable<TeamEventDto>> GenerateTestRegistrationsAsync(Guid eventId, int count, string userId, string? namePrefix = null, bool approve = true);

        // Solo mode
        Task<PlayerEventDto> RegisterPlayerToEventAsync(Guid eventId, RegisterPlayerToEventDto dto, string userId);
        Task<IEnumerable<PlayerEventDto>> GetEventPlayerRegistrationsAsync(Guid eventId);
        Task<TeamEventDto> CreateSoloTemporaryTeamAsync(Guid eventId, CreateSoloTempTeamDto dto, string userId);
    }
}
