using SaaS_BasePlatform.Application.DTOs.HR;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Application.Services
{
    public interface IPaymentPeriodService
    {
        Task<PaymentPeriodDto> GenerateAsync(Guid tenantId, GeneratePaymentPeriodRequestDto request, CancellationToken ct = default);
        Task<PaymentPeriodDto?> GetByIdAsync(Guid tenantId, Guid periodId, CancellationToken ct = default);
        Task<IReadOnlyList<PaymentPeriodSummaryDto>> ListByEmployeeAsync(Guid tenantId, Guid employeeId, CancellationToken ct = default);
        Task<IReadOnlyList<PaymentPeriodSummaryDto>> ListAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task UpdateStatusAsync(Guid tenantId, Guid periodId, PaymentStatus status, CancellationToken ct = default);
        Task DeleteAsync(Guid tenantId, Guid periodId, CancellationToken ct = default);
    }
}
