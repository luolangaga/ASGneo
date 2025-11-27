using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Repositories;
using Microsoft.AspNetCore.Identity;

namespace ASG.Api.Services
{
    public class RecruitmentService : IRecruitmentService
    {
        private readonly IRecruitmentRepository _repo;
        private readonly IEventRepository _eventRepo;
        private readonly IMatchRepository _matchRepo;
        private readonly UserManager<User> _userManager;
        private readonly INotificationService _notify;

        public RecruitmentService(IRecruitmentRepository repo, IEventRepository eventRepo, IMatchRepository matchRepo, UserManager<User> userManager, INotificationService notify)
        { _repo = repo; _eventRepo = eventRepo; _matchRepo = matchRepo; _userManager = userManager; _notify = notify; }

        public async Task<IEnumerable<RecruitmentDto>> GetRecruitmentsAsync(Guid? eventId, string? position, string? q, bool includeClosed = false)
        {
            var list = await _repo.GetRecruitmentsAsync(eventId, position, q, includeClosed);
            return list.Select(Map);
        }

        public async Task<RecruitmentDto?> GetRecruitmentAsync(Guid id)
        {
            var e = await _repo.GetRecruitmentByIdAsync(id);
            return e == null ? null : Map(e);
        }

        public async Task<RecruitmentDto> CreateRecruitmentAsync(CreateRecruitmentDto dto, string userId)
        {
            var ev = await _eventRepo.GetEventByIdAsync(dto.EventId);
            if (ev == null) throw new InvalidOperationException("赛事不存在");
            var u = await _userManager.FindByIdAsync(userId);
            var isAdmin = u != null && (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin);
            var isEventAdmin = ev.EventAdmins != null && ev.EventAdmins.Any(a => a.UserId == userId);
            if (ev.CreatedByUserId != userId && !isAdmin && !isEventAdmin) throw new UnauthorizedAccessException("无权限");
            var task = new RecruitmentTask
            {
                Title = dto.Title,
                EventId = dto.EventId,
                PositionType = Enum.Parse<PositionType>(dto.PositionType, true),
                PayPerMatch = dto.PayPerMatch,
                Slots = dto.Slots,
                Description = dto.Description,
                Status = RecruitmentStatus.Active,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            var created = await _repo.CreateRecruitmentAsync(task);
            if (dto.MatchIds != null && dto.MatchIds.Count > 0)
            {
                var matches = await _matchRepo.GetMatchesByIdsAsync(dto.EventId, dto.MatchIds);
                var useIds = matches.Select(m => m.Id).ToList();
                await _repo.SetTaskMatchesAsync(created.Id, useIds);
            }
            var dtoOut = Map(created);
            dtoOut.MatchIds = await _repo.GetTaskMatchIdsAsync(created.Id);
            return dtoOut;
        }

        public async Task<RecruitmentDto?> UpdateRecruitmentAsync(Guid id, UpdateRecruitmentDto dto, string userId)
        {
            var e = await _repo.GetRecruitmentByIdAsync(id);
            if (e == null) return null;
            var ev = await _eventRepo.GetEventByIdAsync(e.EventId);
            var u = await _userManager.FindByIdAsync(userId);
            var isAdmin = u != null && (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin);
            var isEventAdmin = ev?.EventAdmins != null && ev.EventAdmins.Any(a => a.UserId == userId);
            if (ev?.CreatedByUserId != userId && !isAdmin && !isEventAdmin) throw new UnauthorizedAccessException("无权限");
            e.Title = dto.Title;
            e.EventId = dto.EventId;
            e.PositionType = Enum.Parse<PositionType>(dto.PositionType, true);
            e.PayPerMatch = dto.PayPerMatch;
            e.Slots = dto.Slots;
            e.Description = dto.Description;
            e.Status = Enum.Parse<RecruitmentStatus>(dto.Status, true);
            e.UpdatedAt = DateTime.UtcNow;
            var updated = await _repo.UpdateRecruitmentAsync(e);
            if (dto.MatchIds != null)
            {
                var matches = await _matchRepo.GetMatchesByIdsAsync(dto.EventId, dto.MatchIds);
                var useIds = matches.Select(m => m.Id).ToList();
                await _repo.SetTaskMatchesAsync(updated.Id, useIds);
            }
            var dtoOut = Map(updated);
            dtoOut.MatchIds = await _repo.GetTaskMatchIdsAsync(updated.Id);
            return dtoOut;
        }

        public async Task<bool> DeleteRecruitmentAsync(Guid id, string userId)
        {
            var e = await _repo.GetRecruitmentByIdAsync(id);
            if (e == null) return false;
            var ev = await _eventRepo.GetEventByIdAsync(e.EventId);
            var u = await _userManager.FindByIdAsync(userId);
            var isAdmin = u != null && (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin);
            var isEventAdmin = ev?.EventAdmins != null && ev.EventAdmins.Any(a => a.UserId == userId);
            if (ev?.CreatedByUserId != userId && !isAdmin && !isEventAdmin) throw new UnauthorizedAccessException("无权限");
            return await _repo.DeleteRecruitmentAsync(id);
        }

        public async Task<IEnumerable<RecruitmentApplicationDto>> GetApplicationsAsync(Guid taskId, string userId)
        {
            var task = await _repo.GetRecruitmentByIdAsync(taskId);
            if (task == null) return Enumerable.Empty<RecruitmentApplicationDto>();
            var ev = await _eventRepo.GetEventByIdAsync(task.EventId);
            var u = await _userManager.FindByIdAsync(userId);
            var isAdmin = u != null && (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin);
            var isEventAdmin = ev?.EventAdmins != null && ev.EventAdmins.Any(a => a.UserId == userId);
            if (ev?.CreatedByUserId != userId && !isAdmin && !isEventAdmin) throw new UnauthorizedAccessException("无权限");
            var apps = await _repo.GetApplicationsByTaskAsync(taskId);
            return apps.Select(MapApp);
        }

        public async Task<RecruitmentApplicationDto> ApplyAsync(Guid taskId, ApplyRecruitmentDto dto, string userId)
        {
            var task = await _repo.GetRecruitmentByIdAsync(taskId);
            if (task == null) throw new InvalidOperationException("任务不存在");
            var existing = await _repo.GetApplicationsByTaskAsync(taskId);
            var approvedCount = existing.Count(a => a.Status == ApplicationStatus.Approved);
            if (task.Status == RecruitmentStatus.Closed || approvedCount >= task.Slots) throw new InvalidOperationException("名额已满");
            if (existing.Any(a => a.ApplicantUserId == userId && a.Status != ApplicationStatus.Rejected)) throw new InvalidOperationException("已申请该任务");
            var app = new RecruitmentApplication { TaskId = taskId, ApplicantUserId = userId, Note = dto.Note, Status = ApplicationStatus.Pending, CreatedAt = DateTime.UtcNow };
            var created = await _repo.CreateApplicationAsync(app);
            var ev = await _eventRepo.GetEventByIdAsync(task.EventId);
            var applicant = await _userManager.FindByIdAsync(userId);
            var payload = System.Text.Json.JsonSerializer.Serialize(new { taskId, applicationId = created.Id, applicantUserId = userId, applicantName = applicant?.FullName ?? applicant?.UserName ?? userId, eventId = ev?.Id, eventName = ev?.Name });
            if (ev != null)
            {
                if (!string.IsNullOrEmpty(ev.CreatedByUserId) && ev.CreatedByUserId != userId)
                {
                    await _notify.NotifyUserAsync(ev.CreatedByUserId, "recruitment.application", payload);
                }
                if (ev.EventAdmins != null)
                {
                    foreach (var a in ev.EventAdmins)
                    {
                        if (!string.IsNullOrEmpty(a.UserId) && a.UserId != userId)
                        {
                            await _notify.NotifyUserAsync(a.UserId, "recruitment.application", payload);
                        }
                    }
                }
            }
            return MapApp(created);
        }

        private static RecruitmentDto Map(RecruitmentTask e)
        {
            return new RecruitmentDto
            {
                Id = e.Id,
                Title = e.Title,
                EventId = e.EventId,
                PositionType = e.PositionType.ToString(),
                PayPerMatch = e.PayPerMatch,
                Slots = e.Slots,
                Description = e.Description,
                Status = e.Status.ToString(),
                CreatedByUserId = e.CreatedByUserId,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
            };
        }

        private static RecruitmentApplicationDto MapApp(RecruitmentApplication a)
        {
            return new RecruitmentApplicationDto
            {
                Id = a.Id,
                TaskId = a.TaskId,
                ApplicantUserId = a.ApplicantUserId,
                Note = a.Note,
                Status = a.Status.ToString(),
                CreatedAt = a.CreatedAt,
            };
        }
    }
}
