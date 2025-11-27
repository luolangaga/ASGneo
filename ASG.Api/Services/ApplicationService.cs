using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ASG.Api.Services
{
    public class ApplicationService : IApplicationService
    {
        private readonly IRecruitmentRepository _repo;
        private readonly IEventRepository _eventRepo;
        private readonly IMatchRepository _matchRepo;
        private readonly UserManager<User> _userManager;
        private readonly IEmailService _emailService;

        public ApplicationService(IRecruitmentRepository repo, IEventRepository eventRepo, IMatchRepository matchRepo, UserManager<User> userManager, IEmailService emailService)
        { _repo = repo; _eventRepo = eventRepo; _matchRepo = matchRepo; _userManager = userManager; _emailService = emailService; }

        public async Task<RecruitmentApplicationDto?> ApproveAsync(Guid applicationId, string userId)
        {
            var app = await _repo.GetApplicationByIdAsync(applicationId);
            if (app == null) return null;
            var task = await _repo.GetRecruitmentByIdAsync(app.TaskId);
            if (task == null) return null;
            var ev = await _eventRepo.GetEventByIdAsync(task.EventId);
            var u = await _userManager.FindByIdAsync(userId);
            var isAdmin = u != null && (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin);
            if (ev?.CreatedByUserId != userId && !isAdmin) throw new UnauthorizedAccessException("无权限");
            app.Status = ApplicationStatus.Approved;
            var updated = await _repo.UpdateApplicationAsync(app);
            var allApps = await _repo.GetApplicationsByTaskAsync(app.TaskId);
            var approvedCountNow = allApps.Count(a => a.Status == ApplicationStatus.Approved);
            if (approvedCountNow >= task.Slots && task.Status != RecruitmentStatus.Closed)
            {
                task.Status = RecruitmentStatus.Closed;
                task.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateRecruitmentAsync(task);
            }
            var matchIds = await _repo.GetTaskMatchIdsAsync(app.TaskId);
            if (matchIds.Count > 0)
            {
                var user = await _userManager.FindByIdAsync(app.ApplicantUserId);
                var name = user?.FullName ?? user?.UserName ?? app.ApplicantUserId;
                var matches = await _matchRepo.GetMatchesByIdsAsync(task.EventId, matchIds);
                foreach (var m in matches)
                {
                    if (task.PositionType == PositionType.Commentator) m.Commentator = name;
                    else if (task.PositionType == PositionType.Director) m.Director = name;
                    else if (task.PositionType == PositionType.Referee) m.Referee = name;
                    var dict = ParseCustom(m.CustomData);
                    if (task.PositionType == PositionType.Commentator) dict["commentatorUserId"] = app.ApplicantUserId;
                    if (task.PositionType == PositionType.Director) dict["directorUserId"] = app.ApplicantUserId;
                    if (task.PositionType == PositionType.Referee) dict["refereeUserId"] = app.ApplicantUserId;
                    dict["payPerMatch"] = task.PayPerMatch;
                    m.CustomData = JsonSerializer.Serialize(dict);
                }
                await _matchRepo.UpdateMatchesAsync(matches);
            }
            var applicant = await _userManager.FindByIdAsync(app.ApplicantUserId);
            var toEmail = applicant?.Email;
            if (!string.IsNullOrWhiteSpace(toEmail))
            {
                var subject = "招募申请已通过";
                var html = $"<p>您的申请已通过：{task.Title}</p>";
                _ = _emailService.SendHtmlAsync(toEmail!, subject, html, html);
            }
            DateTime? nextTime = null; string? venue = null;
            if (matchIds.Count > 0)
            {
                var ms = await _matchRepo.GetMatchesByIdsAsync(task.EventId, matchIds);
                var earliest = ms.OrderBy(x => x.MatchTime).FirstOrDefault();
                if (earliest != null)
                {
                    nextTime = earliest.MatchTime;
                    var d = ParseCustom(earliest.CustomData);
                    if (d.TryGetValue("venue", out var v)) venue = v?.ToString();
                }
            }
            return new RecruitmentApplicationDto { Id = updated.Id, TaskId = updated.TaskId, TaskTitle = task.Title, EventId = task.EventId, EventName = ev?.Name ?? string.Empty, NextMatchTime = nextTime, Venue = venue, ApplicantUserId = updated.ApplicantUserId, Note = updated.Note, Status = updated.Status.ToString(), CreatedAt = updated.CreatedAt };
        }

        public async Task<RecruitmentApplicationDto?> RejectAsync(Guid applicationId, string userId)
        {
            var app = await _repo.GetApplicationByIdAsync(applicationId);
            if (app == null) return null;
            var task = await _repo.GetRecruitmentByIdAsync(app.TaskId);
            if (task == null) return null;
            var ev = await _eventRepo.GetEventByIdAsync(task.EventId);
            var u = await _userManager.FindByIdAsync(userId);
            var isAdmin = u != null && (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin);
            if (ev?.CreatedByUserId != userId && !isAdmin) throw new UnauthorizedAccessException("无权限");
            app.Status = ApplicationStatus.Rejected;
            var updated = await _repo.UpdateApplicationAsync(app);
            var applicant = await _userManager.FindByIdAsync(app.ApplicantUserId);
            var toEmail = applicant?.Email;
            if (!string.IsNullOrWhiteSpace(toEmail))
            {
                var subject = "招募申请未通过";
                var html = $"<p>很抱歉，您的申请未通过：{task.Title}</p>";
                _ = _emailService.SendHtmlAsync(toEmail!, subject, html, html);
            }
            var matchIds = await _repo.GetTaskMatchIdsAsync(app.TaskId);
            DateTime? nextTime = null; string? venue = null;
            if (matchIds.Count > 0)
            {
                var matches = await _matchRepo.GetMatchesByIdsAsync(task.EventId, matchIds);
                var earliest = matches.OrderBy(x => x.MatchTime).FirstOrDefault();
                if (earliest != null)
                {
                    nextTime = earliest.MatchTime;
                    var d = ParseCustom(earliest.CustomData);
                    if (d.TryGetValue("venue", out var v)) venue = v?.ToString();
                }
            }
            return new RecruitmentApplicationDto { Id = updated.Id, TaskId = updated.TaskId, TaskTitle = task.Title, EventId = task.EventId, EventName = ev?.Name ?? string.Empty, NextMatchTime = nextTime, Venue = venue, ApplicantUserId = updated.ApplicantUserId, Note = updated.Note, Status = updated.Status.ToString(), CreatedAt = updated.CreatedAt };
        }

        public async Task<bool> SyncMatchesAsync(Guid applicationId, SyncMatchesDto dto, string userId)
        {
            var app = await _repo.GetApplicationByIdAsync(applicationId);
            if (app == null) return false;
            var task = await _repo.GetRecruitmentByIdAsync(app.TaskId);
            if (task == null) return false;
            var ev = await _eventRepo.GetEventByIdAsync(task.EventId);
            var u = await _userManager.FindByIdAsync(userId);
            var isAdmin = u != null && (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin);
            if (ev?.CreatedByUserId != userId && !isAdmin) throw new UnauthorizedAccessException("无权限");
            var applicant = await _userManager.FindByIdAsync(app.ApplicantUserId);
            var name = applicant?.FullName ?? applicant?.UserName ?? app.ApplicantUserId;
            var idSet = dto.MatchIds.ToHashSet();
            var matches = await _matchRepo.GetMatchesByIdsAsync(ev.Id, idSet);
            foreach (var m in matches)
            {
                if (task.PositionType == PositionType.Commentator) m.Commentator = name;
                else if (task.PositionType == PositionType.Director) m.Director = name;
                else if (task.PositionType == PositionType.Referee) m.Referee = name;
                var dict = ParseCustom(m.CustomData);
                if (task.PositionType == PositionType.Commentator) dict["commentatorUserId"] = app.ApplicantUserId;
                if (task.PositionType == PositionType.Director) dict["directorUserId"] = app.ApplicantUserId;
                if (task.PositionType == PositionType.Referee) dict["refereeUserId"] = app.ApplicantUserId;
                dict["payPerMatch"] = task.PayPerMatch;
                m.CustomData = JsonSerializer.Serialize(dict);
            }
            await _matchRepo.UpdateMatchesAsync(matches);
            return true;
        }

        public async Task<IEnumerable<RecruitmentApplicationDto>> GetMyApplicationsAsync(string userId)
        {
            var apps = await _repo.GetApplicationsByUserAsync(userId);
            var taskDict = new Dictionary<Guid, (string Title, Guid EventId, string EventName, DateTime? NextTime, string? Venue)>();
            foreach (var taskId in apps.Select(x => x.TaskId).Distinct())
            {
                var t = await _repo.GetRecruitmentByIdAsync(taskId);
                if (t == null) continue;
                DateTime? nextTime = null; string? venue = null;
                var ids = await _repo.GetTaskMatchIdsAsync(taskId);
                if (ids.Count > 0)
                {
                    var matches = await _matchRepo.GetMatchesByIdsAsync(t.EventId, ids);
                    var earliest = matches.OrderBy(x => x.MatchTime).FirstOrDefault();
                    if (earliest != null)
                    {
                        nextTime = earliest.MatchTime;
                        var d = ParseCustom(earliest.CustomData);
                        if (d.TryGetValue("venue", out var v)) venue = v?.ToString();
                    }
                }
                taskDict[taskId] = (t.Title, t.EventId, (await _eventRepo.GetEventByIdAsync(t.EventId))?.Name ?? string.Empty, nextTime, venue);
            }
            return apps.Select(a => {
                var info = taskDict.TryGetValue(a.TaskId, out var i) ? i : default;
                return new RecruitmentApplicationDto {
                    Id = a.Id,
                    TaskId = a.TaskId,
                    TaskTitle = info.Title,
                    EventId = info.EventId,
                    EventName = info.EventName,
                    NextMatchTime = info.NextTime,
                    Venue = info.Venue,
                    ApplicantUserId = a.ApplicantUserId,
                    Note = a.Note,
                    Status = a.Status.ToString(),
                    CreatedAt = a.CreatedAt
                };
            }).OrderByDescending(x => x.CreatedAt);
        }

        public async Task<IEnumerable<RecruitmentApplicationDto>> GetApplicationsByTaskAsync(Guid taskId, string userId)
        {
            var task = await _repo.GetRecruitmentByIdAsync(taskId);
            if (task == null) return Enumerable.Empty<RecruitmentApplicationDto>();
            var ev = await _eventRepo.GetEventByIdAsync(task.EventId);
            var u = await _userManager.FindByIdAsync(userId);
            var isAdmin = u != null && await _userManager.IsInRoleAsync(u, "Admin");
            if (ev?.CreatedByUserId != userId && !isAdmin) throw new UnauthorizedAccessException("无权限");
            var apps = await _repo.GetApplicationsByTaskAsync(taskId);
            DateTime? nextTime = null; string? venue = null;
            var ids = await _repo.GetTaskMatchIdsAsync(taskId);
            if (ids.Count > 0)
            {
                var matches = await _matchRepo.GetMatchesByIdsAsync(task.EventId, ids);
                var earliest = matches.OrderBy(x => x.MatchTime).FirstOrDefault();
                if (earliest != null)
                {
                    nextTime = earliest.MatchTime;
                    var d = ParseCustom(earliest.CustomData);
                    if (d.TryGetValue("venue", out var v)) venue = v?.ToString();
                }
            }
            return apps.Select(a => new RecruitmentApplicationDto { Id = a.Id, TaskId = a.TaskId, TaskTitle = task.Title, EventId = task.EventId, EventName = ev?.Name ?? string.Empty, NextMatchTime = nextTime, Venue = venue, ApplicantUserId = a.ApplicantUserId, Note = a.Note, Status = a.Status.ToString(), CreatedAt = a.CreatedAt });
        }

        private static Dictionary<string, object> ParseCustom(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new Dictionary<string, object>();
            try { return JsonSerializer.Deserialize<Dictionary<string, object>>(s!) ?? new Dictionary<string, object>(); } catch { return new Dictionary<string, object>(); }
        }

        
    }
}
