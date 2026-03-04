using BiomePampa.Application.DTOs.Employee;
using BiomePampa.Domain.Enums;

namespace BiomePampa.Application.Services
{
    public interface IPaymentPeriodService
    {
        Task<PaymentPeriodDto> CreateAsync(CreatePaymentPeriodDto dto, CancellationToken cancellationToken = default);
        Task<PaymentPeriodDto> GenerateForEmployeeAsync(Guid employeeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<PaymentPeriodDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<PaymentPeriodDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
        Task<IEnumerable<PaymentPeriodSummaryDto>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default);
        Task<PaymentPeriodDto> UpdateStatusAsync(Guid id, PaymentStatus status, CancellationToken cancellationToken = default);
    }
}
