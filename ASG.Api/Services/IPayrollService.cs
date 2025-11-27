using ASG.Api.DTOs;

namespace ASG.Api.Services
{
    public interface IPayrollService
    {
        Task<IEnumerable<PayrollEntryDto>> GetPayrollAsync(string userId, DateTime? from, DateTime? to);
    }
}
