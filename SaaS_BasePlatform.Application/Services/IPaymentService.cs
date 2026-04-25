using SaaS_BasePlatform.Application.DTOs.Employee;

namespace SaaS_BasePlatform.Application.Services
{
    public interface IPaymentService
    {
        Task<PaymentDto> CreateAsync(CreatePaymentDto dto, Guid paidByUserId, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<PaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IEnumerable<PaymentDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
        Task<IEnumerable<PaymentDto>> GetByPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<IEnumerable<PaymentSummaryDto>> GetRecentPaymentsAsync(int count = 10, CancellationToken cancellationToken = default);
    }
}
