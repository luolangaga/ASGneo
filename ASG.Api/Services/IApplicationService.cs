using ASG.Api.DTOs;

namespace ASG.Api.Services
{
    public interface IApplicationService
    {
        Task<RecruitmentApplicationDto?> ApproveAsync(Guid applicationId, string userId);
        Task<RecruitmentApplicationDto?> RejectAsync(Guid applicationId, string userId);
        Task<bool> SyncMatchesAsync(Guid applicationId, SyncMatchesDto dto, string userId);
        Task<IEnumerable<RecruitmentApplicationDto>> GetMyApplicationsAsync(string userId);
        Task<IEnumerable<RecruitmentApplicationDto>> GetApplicationsByTaskAsync(Guid taskId, string userId);
    }
}
