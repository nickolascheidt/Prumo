using SaaS_BasePlatform.Application.DTOs.Employee;

namespace SaaS_BasePlatform.Application.Services
{
    public interface IWorkLogService
    {
        Task<WorkLogDto> CreateAsync(CreateWorkLogDto dto, CancellationToken cancellationToken = default);
        Task<WorkLogDto> UpdateAsync(Guid id, UpdateWorkLogDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<WorkLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<WorkLogDto>> GetByEmployeeIdAsync(Guid employeeId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<WorkLogDto>> GetUnassignedAsync(Guid employeeId, CancellationToken cancellationToken = default);
        Task<IEnumerable<WorkLogDto>> GetCurrentMonthAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    }
}
