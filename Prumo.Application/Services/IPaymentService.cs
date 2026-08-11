using Prumo.Application.DTOs.HR;

namespace Prumo.Application.Services
{
    public interface IPaymentService
    {
        Task<PaymentDto> CreateAsync(Guid tenantId, Guid paidByUserId, CreatePaymentRequestDto request, CancellationToken ct = default);
        Task<PaymentDto?> GetByIdAsync(Guid tenantId, Guid paymentId, CancellationToken ct = default);
        Task<IReadOnlyList<PaymentDto>> ListRecentAsync(Guid tenantId, int count = 20, CancellationToken ct = default);
        Task<IReadOnlyList<PaymentDto>> ListByEmployeeAsync(Guid tenantId, Guid employeeId, CancellationToken ct = default);
        Task DeleteAsync(Guid tenantId, Guid paymentId, CancellationToken ct = default);
    }
}
