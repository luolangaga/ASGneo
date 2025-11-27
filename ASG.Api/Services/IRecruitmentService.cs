using ASG.Api.DTOs;

namespace ASG.Api.Services
{
    public interface IRecruitmentService
    {
        Task<IEnumerable<RecruitmentDto>> GetRecruitmentsAsync(Guid? eventId, string? position, string? q, bool includeClosed = false);
        Task<RecruitmentDto?> GetRecruitmentAsync(Guid id);
        Task<RecruitmentDto> CreateRecruitmentAsync(CreateRecruitmentDto dto, string userId);
        Task<RecruitmentDto?> UpdateRecruitmentAsync(Guid id, UpdateRecruitmentDto dto, string userId);
        Task<bool> DeleteRecruitmentAsync(Guid id, string userId);
        Task<IEnumerable<RecruitmentApplicationDto>> GetApplicationsAsync(Guid taskId, string userId);
        Task<RecruitmentApplicationDto> ApplyAsync(Guid taskId, ApplyRecruitmentDto dto, string userId);
    }
}
