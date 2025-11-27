using ASG.Api.Data;
using ASG.Api.Models;
using ASG.Api.DTOs;
using ASG.Api.Repositories;
using ASG.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DevController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IRoleService _roleService;
        private readonly IUserRepository _userRepository;

        public DevController(ApplicationDbContext context, IWebHostEnvironment env, IRoleService roleService, IUserRepository userRepository)
        {
            _context = context;
            _env = env;
            _roleService = roleService;
            _userRepository = userRepository;
        }

        /// <summary>
        /// 开发环境：灌入测试数据（创建指定数量赛事与战队，并将所有战队报名到首个赛事）
        /// </summary>
        /// <param name="events">赛事数量，默认12</param>
        /// <param name="teams">战队数量，默认15</param>
        /// <returns>插入后的统计信息</returns>
        [HttpPost("seed")]
        public async Task<IActionResult> Seed([FromQuery] int events = 12, [FromQuery] int teams = 15)
        {
            if (!_env.IsDevelopment())
            {
                return StatusCode(403, new { message = "仅在开发环境可用" });
            }

            var now = DateTime.UtcNow;

            // 1) 添加赛事
            var createdEvents = new List<Event>();
            for (int i = 1; i <= events; i++)
            {
                var ev = new Event
                {
                    Id = Guid.NewGuid(),
                    Name = $"开发赛 {DateTime.UtcNow:MMddHHmm}-{i}",
                    Description = $"开发测试赛事 {i}",
                    RegistrationStartTime = now.AddDays(-2),
                    RegistrationEndTime = now.AddDays(14),
                    CompetitionStartTime = now.AddDays(20 + i),
                    CompetitionEndTime = now.AddDays(23 + i),
                    MaxTeams = 128,
                    Status = EventStatus.RegistrationOpen,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                createdEvents.Add(ev);
            }
            await _context.Events.AddRangeAsync(createdEvents);
            await _context.SaveChangesAsync();

            // 选择报名目标赛事（刚创建的第一个）
            var targetEvent = createdEvents.First();

            // 2) 添加战队
            var createdTeams = new List<Team>();
            for (int i = 1; i <= teams; i++)
            {
                var team = new Team
                {
                    Id = Guid.NewGuid(),
                    Name = $"开发战队 {DateTime.UtcNow:MMddHHmm}-{i}",
                    Password = "pass1234",
                    Description = $"开发测试战队 {i}",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                createdTeams.Add(team);
            }
            await _context.Teams.AddRangeAsync(createdTeams);
            await _context.SaveChangesAsync();

            // 3) 全部报名到目标赛事
            var registrations = createdTeams.Select(t => new TeamEvent
            {
                TeamId = t.Id,
                EventId = targetEvent.Id,
                RegistrationTime = now,
                Status = RegistrationStatus.Approved,
                Notes = "开发测试：自动报名"
            }).ToList();
            await _context.TeamEvents.AddRangeAsync(registrations);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                createdEvents = createdEvents.Count,
                createdTeams = createdTeams.Count,
                registrations = registrations.Count,
                targetEventId = targetEvent.Id,
                targetEventName = targetEvent.Name
            });
        }

        /// <summary>
        /// 开发环境：将邮箱为指定地址的用户赋予管理员角色（Admin）
        /// 固定邮箱：luolan233@outlook.com
        /// </summary>
        [HttpPost("grant-admin")]
        public async Task<IActionResult> GrantAdminToOutlook()
        {
          

            var email = "luolan233@outlook.com";
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                return NotFound(new { message = "未找到该邮箱的用户", email });
            }

            var oldRole = user.Role;
            if (oldRole == UserRole.Admin || oldRole == UserRole.SuperAdmin)
            {
                return Ok(new { updated = false, message = "用户已是管理员", userId = user.Id, email, role = user.Role.ToString() });
            }

            var updateDto = new UpdateUserRoleDto { UserId = user.Id, Role = UserRole.Admin };
            var result = await _roleService.UpdateUserRoleAsync(updateDto);
            if (result == null)
            {
                return BadRequest(new { updated = false, message = "更新角色失败" });
            }

            return Ok(new { updated = true, userId = result.Id, email = result.Email, oldRole = oldRole.ToString(), newRole = result.Role.ToString() });
        }
    }
}
