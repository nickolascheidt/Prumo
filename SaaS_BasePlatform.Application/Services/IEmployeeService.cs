using SaaS_BasePlatform.Application.DTOs.Employee;

namespace SaaS_BasePlatform.Application.Services
{
    public interface IEmployeeService
    {
        Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto, CancellationToken cancellationToken = default);
        Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<EmployeeDto?> GetByCPFAsync(string cpf, CancellationToken cancellationToken = default);
        Task<IEnumerable<EmployeeDto>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
        Task<IEnumerable<EmployeeSummaryDto>> GetSummariesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    }
}
