using Prumo.Application.DTOs.HR;

namespace Prumo.Application.Services
{
    public interface IWorkLogService
    {
        Task<IReadOnlyList<WorkLogDto>> ListByEmployeeAsync(Guid tenantId, Guid employeeId, WorkLogQueryDto query, CancellationToken ct = default);
        Task<WorkLogDto?> GetByIdAsync(Guid tenantId, Guid workLogId, CancellationToken ct = default);
        Task<WorkLogDto> CreateAsync(Guid tenantId, CreateWorkLogRequestDto request, CancellationToken ct = default);
        Task<WorkLogDto> UpdateAsync(Guid tenantId, Guid workLogId, UpdateWorkLogRequestDto request, CancellationToken ct = default);
        Task DeleteAsync(Guid tenantId, Guid workLogId, CancellationToken ct = default);
    }
}
