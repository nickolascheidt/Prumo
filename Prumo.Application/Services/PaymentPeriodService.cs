using Microsoft.EntityFrameworkCore;
using Prumo.Application.DTOs.HR;
using Prumo.Domain.Entities;
using Prumo.Domain.Enums;
using Prumo.Infrastructure.Data;

namespace Prumo.Application.Services
{
    public class PaymentPeriodService : IPaymentPeriodService
    {
        private readonly ApplicationDbContext _db;

        public PaymentPeriodService(ApplicationDbContext db) => _db = db;

        public async Task<PaymentPeriodDto> GenerateAsync(
            Guid tenantId, GeneratePaymentPeriodRequestDto request, CancellationToken ct = default)
        {
            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct)
                ?? throw new KeyNotFoundException("Employee not found.");

            var startDate = DateTime.SpecifyKind(request.StartDate.Date, DateTimeKind.Utc);
            var endDate   = DateTime.SpecifyKind(request.EndDate.Date,   DateTimeKind.Utc);

            if (startDate >= endDate)
                throw new ArgumentException("StartDate must be before EndDate.");

            var duplicate = await _db.PaymentPeriods
                .AnyAsync(p => p.EmployeeId == request.EmployeeId
                            && p.StartDate == startDate
                            && p.EndDate   == endDate, ct);
            if (duplicate)
                throw new InvalidOperationException(
                    "A payment period for this employee and date range already exists.");

            var workLogs = await _db.WorkLogs
                .Where(w => w.EmployeeId == request.EmployeeId
                         && w.WorkDate >= startDate
                         && w.WorkDate <= endDate
                         && w.PaymentPeriodId == null)
                .ToListAsync(ct);

            if (!workLogs.Any())
                throw new InvalidOperationException(
                    "No unassigned work logs found for the specified date range.");

            var (totalHours, totalAmount) = PaymentPeriod.CalculateSummary(workLogs);

            var period = new PaymentPeriod
            {
                EmployeeId  = request.EmployeeId,
                StartDate   = startDate,
                EndDate     = endDate,
                TotalHours  = totalHours,
                TotalAmount = totalAmount,
                Status      = PaymentStatus.Pending
            };

            _db.PaymentPeriods.Add(period);

            foreach (var wl in workLogs)
                wl.PaymentPeriodId = period.Id;

            await _db.SaveChangesAsync(ct);

            return await GetByIdAsync(tenantId, period.Id, ct)
                ?? throw new InvalidOperationException("Failed to read created payment period.");
        }

        public async Task<PaymentPeriodDto?> GetByIdAsync(
            Guid tenantId, Guid periodId, CancellationToken ct = default)
        {
            var period = await _db.PaymentPeriods
                .Include(p => p.Employee)
                .Include(p => p.WorkLogs)
                .FirstOrDefaultAsync(p => p.Id == periodId, ct);
            return period == null ? null : ToDto(period);
        }

        public async Task<IReadOnlyList<PaymentPeriodSummaryDto>> ListByEmployeeAsync(
            Guid tenantId, Guid employeeId, CancellationToken ct = default)
        {
            // A semantics guard, not a tenant guard: without it, asking for the periods of
            // another tenant's employee would return 200 with an empty list instead of 404.
            var exists = await _db.Employees.AnyAsync(e => e.Id == employeeId, ct);
            if (!exists) throw new KeyNotFoundException("Employee not found.");

            return await _db.PaymentPeriods
                .Include(p => p.Employee)
                .Where(p => p.EmployeeId == employeeId)
                .OrderByDescending(p => p.StartDate)
                .Select(p => ToSummaryDto(p))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<PaymentPeriodSummaryDto>> ListAllByTenantAsync(
            Guid tenantId, CancellationToken ct = default)
        {
            var periods = await _db.PaymentPeriods
                .Include(p => p.Employee)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync(ct);
            return periods.Select(p => ToSummaryDto(p)).ToList();
        }

        public async Task UpdateStatusAsync(
            Guid tenantId, Guid periodId, PaymentStatus status, CancellationToken ct = default)
        {
            var period = await _db.PaymentPeriods
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.Id == periodId, ct)
                ?? throw new KeyNotFoundException("Payment period not found.");
            period.Status = status;
            await _db.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(
            Guid tenantId, Guid periodId, CancellationToken ct = default)
        {
            var period = await _db.PaymentPeriods
                .Include(p => p.Employee)
                .Include(p => p.Payment)
                .FirstOrDefaultAsync(p => p.Id == periodId, ct)
                ?? throw new KeyNotFoundException("Payment period not found.");

            if (period.Payment != null)
                throw new InvalidOperationException(
                    "Cannot delete a payment period that has an associated payment.");

            await _db.WorkLogs
                .Where(w => w.PaymentPeriodId == periodId)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.PaymentPeriodId, (Guid?)null), ct);

            _db.PaymentPeriods.Remove(period);
            await _db.SaveChangesAsync(ct);
        }

        private static PaymentPeriodDto ToDto(PaymentPeriod p) => new(
            p.Id, p.EmployeeId, p.Employee.FullName,
            p.StartDate, p.EndDate, p.TotalHours, p.TotalAmount,
            (int)p.Status, p.Status.ToString(),
            p.WorkLogs.Select(w => new WorkLogDto(
                w.Id, w.EmployeeId, p.Employee.FullName,
                w.WorkDate, w.HoursWorked, w.HourlyRateAtTime, w.TotalAmount,
                w.Notes, w.PaymentPeriodId, w.CreatedAt)).ToList(),
            p.CreatedAt);

        private static PaymentPeriodSummaryDto ToSummaryDto(PaymentPeriod p) => new(
            p.Id, p.EmployeeId, p.Employee.FullName,
            p.StartDate, p.EndDate, p.TotalHours, p.TotalAmount,
            (int)p.Status, p.Status.ToString(), p.CreatedAt);
    }
}
