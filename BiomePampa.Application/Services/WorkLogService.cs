using BiomePampa.Application.DTOs.Employee;
using BiomePampa.Domain.Entities;
using BiomePampa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BiomePampa.Application.Services
{
    public class WorkLogService : IWorkLogService
    {
        private readonly ApplicationDbContext _context;

        public WorkLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<WorkLogDto> CreateAsync(CreateWorkLogDto dto, CancellationToken cancellationToken = default)
        {
            var employee = await _context.Employees.FindAsync(new object[] { dto.EmployeeId }, cancellationToken);
            if (employee == null)
                throw new KeyNotFoundException("Funcionário não encontrado.");

            // Verificar se já existe registro para esta data
            var existingLog = await _context.WorkLogs
                .FirstOrDefaultAsync(w => w.EmployeeId == dto.EmployeeId && w.WorkDate.Date == dto.WorkDate.Date, cancellationToken);

            if (existingLog != null)
                throw new InvalidOperationException("Já existe um registro de horas para este funcionário nesta data.");

            var workLog = new WorkLog
            {
                EmployeeId = dto.EmployeeId,
                WorkDate = dto.WorkDate.Date,
                HoursWorked = dto.HoursWorked,
                HourlyRateAtTime = employee.HourlyRate,
                TotalAmount = dto.HoursWorked * employee.HourlyRate,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.WorkLogs.Add(workLog);
            await _context.SaveChangesAsync(cancellationToken);

            return await MapToDtoAsync(workLog, cancellationToken);
        }

        public async Task<WorkLogDto> UpdateAsync(Guid id, UpdateWorkLogDto dto, CancellationToken cancellationToken = default)
        {
            var workLog = await _context.WorkLogs
                .Include(w => w.Employee)
                .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

            if (workLog == null)
                throw new KeyNotFoundException("Registro de horas não encontrado.");

            if (workLog.PaymentPeriodId != null)
                throw new InvalidOperationException("Não é possível editar um registro já vinculado a um período de pagamento.");

            workLog.WorkDate = dto.WorkDate.Date;
            workLog.HoursWorked = dto.HoursWorked;
            workLog.TotalAmount = dto.HoursWorked * workLog.HourlyRateAtTime;
            workLog.Notes = dto.Notes;
            workLog.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return await MapToDtoAsync(workLog, cancellationToken);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var workLog = await _context.WorkLogs.FindAsync(new object[] { id }, cancellationToken);
            if (workLog == null)
                return false;

            if (workLog.PaymentPeriodId != null)
                throw new InvalidOperationException("Não é possível excluir um registro já vinculado a um período de pagamento.");

            _context.WorkLogs.Remove(workLog);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<WorkLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var workLog = await _context.WorkLogs
                .Include(w => w.Employee)
                .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

            return workLog == null ? null : await MapToDtoAsync(workLog, cancellationToken);
        }

        public async Task<IEnumerable<WorkLogDto>> GetByEmployeeIdAsync(Guid employeeId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            var query = _context.WorkLogs
                .Include(w => w.Employee)
                .Where(w => w.EmployeeId == employeeId);

            if (startDate.HasValue)
                query = query.Where(w => w.WorkDate >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(w => w.WorkDate <= endDate.Value.Date);

            var workLogs = await query
                .OrderByDescending(w => w.WorkDate)
                .ToListAsync(cancellationToken);

            var dtos = new List<WorkLogDto>();
            foreach (var log in workLogs)
            {
                dtos.Add(await MapToDtoAsync(log, cancellationToken));
            }

            return dtos;
        }

        public async Task<IEnumerable<WorkLogDto>> GetUnassignedAsync(Guid employeeId, CancellationToken cancellationToken = default)
        {
            var workLogs = await _context.WorkLogs
                .Include(w => w.Employee)
                .Where(w => w.EmployeeId == employeeId && w.PaymentPeriodId == null)
                .OrderBy(w => w.WorkDate)
                .ToListAsync(cancellationToken);

            var dtos = new List<WorkLogDto>();
            foreach (var log in workLogs)
            {
                dtos.Add(await MapToDtoAsync(log, cancellationToken));
            }

            return dtos;
        }

        private async Task<WorkLogDto> MapToDtoAsync(WorkLog workLog, CancellationToken cancellationToken)
        {
            if (workLog.Employee == null)
            {
                var employee = await _context.Employees.FindAsync(new object[] { workLog.EmployeeId }, cancellationToken);
                workLog.Employee = employee!;
            }

            return new WorkLogDto(
                workLog.Id,
                workLog.EmployeeId,
                workLog.Employee.FullName,
                workLog.WorkDate,
                workLog.HoursWorked,
                workLog.HourlyRateAtTime,
                workLog.TotalAmount,
                workLog.Notes,
                workLog.PaymentPeriodId,
                workLog.CreatedAt
            );
        }
    }
}
