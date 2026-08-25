using Microsoft.EntityFrameworkCore;
using Prumo.Application.DTOs.HR;
using Prumo.Domain.Entities;
using Prumo.Infrastructure.Data;

namespace Prumo.Application.Services
{
    public class WorkLogService : IWorkLogService
    {
        private readonly ApplicationDbContext _db;

        public WorkLogService(ApplicationDbContext db) => _db = db;

        public async Task<IReadOnlyList<WorkLogDto>> ListByEmployeeAsync(
            Guid tenantId, Guid employeeId, WorkLogQueryDto query, CancellationToken ct = default)
        {
            await RequireEmployeeAsync(tenantId, employeeId, ct);

            var q = _db.WorkLogs
                .Include(w => w.Employee)
                .Where(w => w.EmployeeId == employeeId);

            if (query.From.HasValue) q = q.Where(w => w.WorkDate >= query.From.Value.Date);
            if (query.To.HasValue)   q = q.Where(w => w.WorkDate <= query.To.Value.Date);
            if (query.OnlyUnassigned) q = q.Where(w => w.PaymentPeriodId == null);

            return await q.OrderByDescending(w => w.WorkDate)
                .Select(w => ToDto(w))
                .ToListAsync(ct);
        }

        public async Task<WorkLogDto?> GetByIdAsync(
            Guid tenantId, Guid workLogId, CancellationToken ct = default)
        {
            var w = await _db.WorkLogs
                .Include(w => w.Employee)
                .FirstOrDefaultAsync(w => w.Id == workLogId, ct);
            return w == null ? null : ToDto(w);
        }

        public async Task<WorkLogDto> CreateAsync(
            Guid tenantId, CreateWorkLogRequestDto request, CancellationToken ct = default)
        {
            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct)
                ?? throw new KeyNotFoundException("Employee not found.");

            if (!employee.IsActive)
                throw new InvalidOperationException("Cannot log hours for an inactive employee.");

            if (request.HoursWorked <= 0 || request.HoursWorked > 24)
                throw new ArgumentException("HoursWorked must be between 0 and 24.");

            var workDate = DateTime.SpecifyKind(request.WorkDate.Date, DateTimeKind.Utc);

            var duplicate = await _db.WorkLogs
                .AnyAsync(w => w.EmployeeId == request.EmployeeId && w.WorkDate == workDate, ct);
            if (duplicate)
                throw new InvalidOperationException(
                    $"A work log already exists for {employee.FullName} on {workDate:yyyy-MM-dd}.");

            var workLog = new WorkLog
            {
                EmployeeId       = request.EmployeeId,
                WorkDate         = workDate,
                HoursWorked      = request.HoursWorked,
                HourlyRateAtTime = employee.HourlyRate,
                TotalAmount      = WorkLog.CalculateTotalAmount(request.HoursWorked, employee.HourlyRate),
                Notes            = request.Notes?.Trim()
            };

            _db.WorkLogs.Add(workLog);
            await _db.SaveChangesAsync(ct);
            return new WorkLogDto(
                workLog.Id, workLog.EmployeeId, employee.FullName,
                workLog.WorkDate, workLog.HoursWorked, workLog.HourlyRateAtTime,
                workLog.TotalAmount, workLog.Notes, workLog.PaymentPeriodId, workLog.CreatedAt);
        }

        public async Task<WorkLogDto> UpdateAsync(
            Guid tenantId, Guid workLogId, UpdateWorkLogRequestDto request, CancellationToken ct = default)
        {
            var workLog = await _db.WorkLogs
                .Include(w => w.Employee)
                .FirstOrDefaultAsync(w => w.Id == workLogId, ct)
                ?? throw new KeyNotFoundException("WorkLog not found.");

            if (workLog.PaymentPeriodId.HasValue)
                throw new InvalidOperationException(
                    "Cannot edit a work log that is linked to a payment period.");

            if (request.HoursWorked <= 0 || request.HoursWorked > 24)
                throw new ArgumentException("HoursWorked must be between 0 and 24.");

            var workDate = DateTime.SpecifyKind(request.WorkDate.Date, DateTimeKind.Utc);

            if (workDate != workLog.WorkDate)
            {
                var duplicate = await _db.WorkLogs
                    .AnyAsync(w => w.EmployeeId == workLog.EmployeeId
                                && w.WorkDate == workDate
                                && w.Id != workLogId, ct);
                if (duplicate)
                    throw new InvalidOperationException(
                        $"A work log already exists for {workLog.Employee.FullName} on {workDate:yyyy-MM-dd}.");
            }

            workLog.WorkDate    = workDate;
            workLog.HoursWorked = request.HoursWorked;
            workLog.TotalAmount = WorkLog.CalculateTotalAmount(request.HoursWorked, workLog.HourlyRateAtTime);
            workLog.Notes       = request.Notes?.Trim();

            await _db.SaveChangesAsync(ct);
            return ToDto(workLog);
        }

        public async Task DeleteAsync(
            Guid tenantId, Guid workLogId, CancellationToken ct = default)
        {
            var workLog = await _db.WorkLogs
                .Include(w => w.Employee)
                .FirstOrDefaultAsync(w => w.Id == workLogId, ct)
                ?? throw new KeyNotFoundException("WorkLog not found.");

            if (workLog.PaymentPeriodId.HasValue)
                throw new InvalidOperationException(
                    "Cannot delete a work log that is linked to a payment period.");

            _db.WorkLogs.Remove(workLog);
            await _db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Não é mais a proteção de tenant — desde a fase 2 o filtro por navegação em
        /// WorkLog cuida disso. Continua existindo pela semântica de erro: sem este guard,
        /// pedir horas de um employee de outro tenant devolveria 200 com lista vazia em vez
        /// de 404, vazando a informação de que o employee não existe *para você*.
        /// </summary>
        private async Task RequireEmployeeAsync(Guid tenantId, Guid employeeId, CancellationToken ct)
        {
            var exists = await _db.Employees.AnyAsync(e => e.Id == employeeId, ct);
            if (!exists) throw new KeyNotFoundException("Employee not found.");
        }

        private static WorkLogDto ToDto(WorkLog w) => new(
            w.Id, w.EmployeeId, w.Employee?.FullName ?? string.Empty,
            w.WorkDate, w.HoursWorked, w.HourlyRateAtTime, w.TotalAmount,
            w.Notes, w.PaymentPeriodId, w.CreatedAt);
    }
}
