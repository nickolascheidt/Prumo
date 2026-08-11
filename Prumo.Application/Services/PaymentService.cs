using Microsoft.EntityFrameworkCore;
using Prumo.Application.DTOs.HR;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;

namespace Prumo.Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _db;

        public PaymentService(ApplicationDbContext db) => _db = db;

        public async Task<PaymentDto> CreateAsync(
            Guid tenantId, Guid paidByUserId, CreatePaymentRequestDto request, CancellationToken ct = default)
        {
            if (!Enum.IsDefined(typeof(HrPaymentMethod), request.PaymentMethod))
                throw new ArgumentException($"Invalid PaymentMethod: {request.PaymentMethod}.");

            var period = await _db.PaymentPeriods.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Include(p => p.Payment)
                .FirstOrDefaultAsync(p => p.Id == request.PaymentPeriodId
                                       && p.Employee.TenantId == tenantId, ct)
                ?? throw new KeyNotFoundException("Payment period not found.");

            if (period.Payment != null)
                throw new InvalidOperationException(
                    "This payment period already has an associated payment.");

            if (period.Status != PaymentStatus.Pending)
                throw new InvalidOperationException(
                    "Only Pending payment periods can be paid.");

            var payment = new Payment
            {
                EmployeeId      = period.EmployeeId,
                PaymentPeriodId = period.Id,
                PaymentDate     = DateTime.SpecifyKind(request.PaymentDate, DateTimeKind.Utc),
                Amount          = period.TotalAmount,
                PaymentMethod   = (HrPaymentMethod)request.PaymentMethod,
                PaymentProof    = request.PaymentProof?.Trim(),
                Notes           = request.Notes?.Trim(),
                PaidByUserId    = paidByUserId
            };

            period.Status = PaymentStatus.Paid;
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync(ct);

            return await GetByIdAsync(tenantId, payment.Id, ct)
                ?? throw new InvalidOperationException("Failed to read created payment.");
        }

        public async Task<PaymentDto?> GetByIdAsync(
            Guid tenantId, Guid paymentId, CancellationToken ct = default)
        {
            var p = await _db.Payments.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Include(p => p.PaidByUser)
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.Employee.TenantId == tenantId, ct);
            return p == null ? null : ToDto(p);
        }

        public async Task<IReadOnlyList<PaymentDto>> ListRecentAsync(
            Guid tenantId, int count = 20, CancellationToken ct = default)
        {
            return await _db.Payments.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Include(p => p.PaidByUser)
                .Where(p => p.Employee.TenantId == tenantId)
                .OrderByDescending(p => p.PaymentDate)
                .Take(Math.Min(count, 100))
                .Select(p => ToDto(p))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<PaymentDto>> ListByEmployeeAsync(
            Guid tenantId, Guid employeeId, CancellationToken ct = default)
        {
            var exists = await _db.Employees.IgnoreQueryFilters()
                .AnyAsync(e => e.TenantId == tenantId && e.Id == employeeId, ct);
            if (!exists) throw new KeyNotFoundException("Employee not found.");

            return await _db.Payments.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Include(p => p.PaidByUser)
                .Where(p => p.EmployeeId == employeeId)
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => ToDto(p))
                .ToListAsync(ct);
        }

        public async Task DeleteAsync(
            Guid tenantId, Guid paymentId, CancellationToken ct = default)
        {
            var payment = await _db.Payments.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Include(p => p.PaymentPeriod)
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.Employee.TenantId == tenantId, ct)
                ?? throw new KeyNotFoundException("Payment not found.");

            payment.PaymentPeriod.Status = PaymentStatus.Pending;
            _db.Payments.Remove(payment);
            await _db.SaveChangesAsync(ct);
        }

        private static PaymentDto ToDto(Payment p) => new(
            p.Id, p.EmployeeId, p.Employee.FullName,
            p.PaymentPeriodId, p.PaymentDate, p.Amount,
            (int)p.PaymentMethod, p.PaymentMethod.ToString(),
            p.PaymentProof, p.Notes,
            p.PaidByUserId, p.PaidByUser?.FullName ?? string.Empty,
            p.CreatedAt);
    }
}
