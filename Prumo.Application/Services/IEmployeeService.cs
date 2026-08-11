using Prumo.Application.DTOs.HR;

namespace Prumo.Application.Services
{
    public interface IEmployeeService
    {
        Task<IReadOnlyList<EmployeeDto>> ListAsync(Guid tenantId, bool includeInactive = false, CancellationToken ct = default);
        Task<EmployeeDto?> GetAsync(Guid tenantId, Guid employeeId, CancellationToken ct = default);
        Task<EmployeeDto> CreateAsync(Guid tenantId, CreateEmployeeRequestDto request, CancellationToken ct = default);
        Task<EmployeeDto> UpdateAsync(Guid tenantId, Guid employeeId, UpdateEmployeeRequestDto request, CancellationToken ct = default);
        Task DeactivateAsync(Guid tenantId, Guid employeeId, CancellationToken ct = default);
    }
}
