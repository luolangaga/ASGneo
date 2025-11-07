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
        /// 团队报名赛事
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <param name="registerDto">报名DTO</param>
        /// <param name="userId">操作用户ID</param>
        /// <returns>报名信息DTO</returns>
        Task<TeamEventDto?> RegisterTeamToEventAsync(Guid eventId, RegisterTeamToEventDto registerDto, string userId);

        /// <summary>
        /// 取消团队报名
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <param name="teamId">团队ID</param>
        /// <param name="userId">操作用户ID</param>
        /// <returns>是否取消成功</returns>
        Task<bool> UnregisterTeamFromEventAsync(Guid eventId, Guid teamId, string userId);

        /// <summary>
        /// 更新团队报名状态
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <param name="teamId">团队ID</param>
        /// <param name="updateDto">更新DTO</param>
        /// <param name="userId">操作用户ID</param>
        /// <returns>更新的报名信息DTO</returns>
        Task<TeamEventDto?> UpdateTeamRegistrationStatusAsync(Guid eventId, Guid teamId, UpdateTeamRegistrationDto updateDto, string userId);

        /// <summary>
        /// 获取赛事的报名团队
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <returns>报名团队DTO列表</returns>
        Task<IEnumerable<TeamEventDto>> GetEventRegistrationsAsync(Guid eventId);

        /// <summary>
        /// 获取团队的报名赛事
        /// </summary>
        /// <param name="teamId">团队ID</param>
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
        /// 获取即将开始的赛事
        /// </summary>
        /// <returns>即将开始的赛事DTO列表</returns>
        Task<IEnumerable<EventDto>> GetUpcomingEventsAsync();

        /// <summary>
        /// 验证用户是否有权限操作赛事
        /// </summary>
        /// <param name="eventId">赛事ID</param>
        /// <param name="userId">用户ID</param>
        /// <returns>是否有权限</returns>
        Task<bool> CanUserManageEventAsync(Guid eventId, string userId);

        /// <summary>
        /// 验证用户是否有权限操作团队报名
        /// </summary>
        /// <param name="teamId">团队ID</param>
        /// <param name="userId">用户ID</param>
        /// <returns>是否有权限</returns>
        Task<bool> CanUserManageTeamRegistrationAsync(Guid teamId, string userId);
    }
}