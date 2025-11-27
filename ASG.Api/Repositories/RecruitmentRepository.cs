using ASG.Api.Data;
using ASG.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ASG.Api.Repositories
{
    public class RecruitmentRepository : IRecruitmentRepository
    {
        private readonly ApplicationDbContext _db;
        public RecruitmentRepository(ApplicationDbContext db) { _db = db; }

        private void EnsureTables()
        {
            _db.Database.EnsureCreated();
        }

        public async Task<IEnumerable<RecruitmentTask>> GetRecruitmentsAsync(Guid? eventId, string? position, string? q, bool includeClosed = false)
        {
            try
            {
                var query = _db.RecruitmentTasks.AsQueryable();
                if (eventId.HasValue) query = query.Where(x => x.EventId == eventId.Value);
                if (!string.IsNullOrWhiteSpace(position)) query = query.Where(x => x.PositionType.ToString() == position);
                if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Title.Contains(q) || (x.Description ?? "").Contains(q));
                if (!includeClosed) query = query.Where(x => x.Status == RecruitmentStatus.Active);
                query = query.OrderByDescending(x => x.CreatedAt);
                return await query.ToListAsync();
            }
            catch
            {
                EnsureTables();
                var query = _db.RecruitmentTasks.AsQueryable();
                if (eventId.HasValue) query = query.Where(x => x.EventId == eventId.Value);
                if (!string.IsNullOrWhiteSpace(position)) query = query.Where(x => x.PositionType.ToString() == position);
                if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Title.Contains(q) || (x.Description ?? "").Contains(q));
                if (!includeClosed) query = query.Where(x => x.Status == RecruitmentStatus.Active);
                query = query.OrderByDescending(x => x.CreatedAt);
                return await query.ToListAsync();
            }
        }

        public Task<RecruitmentTask?> GetRecruitmentByIdAsync(Guid id) => _db.RecruitmentTasks.FirstOrDefaultAsync(x => x.Id == id);
        public async Task<RecruitmentTask> CreateRecruitmentAsync(RecruitmentTask task) { EnsureTables(); _db.RecruitmentTasks.Add(task); await _db.SaveChangesAsync(); return task; }
        public async Task<RecruitmentTask> UpdateRecruitmentAsync(RecruitmentTask task) { _db.RecruitmentTasks.Update(task); await _db.SaveChangesAsync(); return task; }
        public async Task<bool> DeleteRecruitmentAsync(Guid id) { var e = await _db.RecruitmentTasks.FindAsync(id); if (e == null) return false; _db.RecruitmentTasks.Remove(e); await _db.SaveChangesAsync(); return true; }

        public async Task<IEnumerable<RecruitmentApplication>> GetApplicationsByTaskAsync(Guid taskId)
        {
            return await _db.RecruitmentApplications.Where(x => x.TaskId == taskId).OrderByDescending(x => x.CreatedAt).ToListAsync();
        }

        public async Task<IEnumerable<RecruitmentApplication>> GetApplicationsByUserAsync(string userId)
        {
            return await _db.RecruitmentApplications.Where(x => x.ApplicantUserId == userId).OrderByDescending(x => x.CreatedAt).ToListAsync();
        }
        public async Task<RecruitmentApplication> CreateApplicationAsync(RecruitmentApplication app) { _db.RecruitmentApplications.Add(app); await _db.SaveChangesAsync(); return app; }
        public Task<RecruitmentApplication?> GetApplicationByIdAsync(Guid id) => _db.RecruitmentApplications.FirstOrDefaultAsync(x => x.Id == id);
        public async Task<RecruitmentApplication> UpdateApplicationAsync(RecruitmentApplication app) { _db.RecruitmentApplications.Update(app); await _db.SaveChangesAsync(); return app; }

        public async Task<List<Guid>> GetTaskMatchIdsAsync(Guid taskId)
        {
            return await _db.RecruitmentTaskMatches.Where(x => x.TaskId == taskId).Select(x => x.MatchId).ToListAsync();
        }

        public async Task SetTaskMatchesAsync(Guid taskId, IEnumerable<Guid> matchIds)
        {
            var existing = _db.RecruitmentTaskMatches.Where(x => x.TaskId == taskId);
            _db.RecruitmentTaskMatches.RemoveRange(existing);
            var toAdd = matchIds.Distinct().Select(id => new RecruitmentTaskMatch { TaskId = taskId, MatchId = id }).ToList();
            if (toAdd.Count > 0) await _db.RecruitmentTaskMatches.AddRangeAsync(toAdd);
            await _db.SaveChangesAsync();
        }
    }
}
