using ASG.Api.Models;

namespace ASG.Api.Repositories
{
    public interface IRecruitmentRepository
    {
        Task<IEnumerable<RecruitmentTask>> GetRecruitmentsAsync(Guid? eventId, string? position, string? q, bool includeClosed = false);
        Task<RecruitmentTask?> GetRecruitmentByIdAsync(Guid id);
        Task<RecruitmentTask> CreateRecruitmentAsync(RecruitmentTask task);
        Task<RecruitmentTask> UpdateRecruitmentAsync(RecruitmentTask task);
        Task<bool> DeleteRecruitmentAsync(Guid id);
        Task<IEnumerable<RecruitmentApplication>> GetApplicationsByTaskAsync(Guid taskId);
        Task<IEnumerable<RecruitmentApplication>> GetApplicationsByUserAsync(string userId);
        Task<RecruitmentApplication> CreateApplicationAsync(RecruitmentApplication app);
        Task<RecruitmentApplication?> GetApplicationByIdAsync(Guid id);
        Task<RecruitmentApplication> UpdateApplicationAsync(RecruitmentApplication app);
        Task<List<Guid>> GetTaskMatchIdsAsync(Guid taskId);
        Task SetTaskMatchesAsync(Guid taskId, IEnumerable<Guid> matchIds);
    }
}
