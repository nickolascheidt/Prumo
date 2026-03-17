using BiomePampa.Application.DTOs.Employee;
using BiomePampa.Domain.Entities;
using BiomePampa.Domain.Enums;
using BiomePampa.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BiomePampa.Application.Services
{
    public class PaymentPeriodService : IPaymentPeriodService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PaymentPeriodService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PaymentPeriodDto> CreateAsync(CreatePaymentPeriodDto dto, CancellationToken cancellationToken = default)
        {
            return await GenerateForEmployeeAsync(dto.EmployeeId, dto.StartDate, dto.EndDate, cancellationToken);
        }

        public async Task<PaymentPeriodDto> GenerateForEmployeeAsync(Guid employeeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(employeeId, cancellationToken);
            if (employee == null)
                throw new KeyNotFoundException("Funcionário não encontrado.");

            // Verificar se já existe período para estas datas
            var existingPeriods = await _unitOfWork.Repository<PaymentPeriod>()
                .FindAsync(p => p.EmployeeId == employeeId && p.StartDate == startDate.Date && p.EndDate == endDate.Date, cancellationToken);

            if (existingPeriods.Any())
                throw new InvalidOperationException("Já existe um período de pagamento para estas datas.");

            // Buscar trabalhos não atribuídos no período
            var workLogs = await _unitOfWork.Context.WorkLogs
                .Where(w => w.EmployeeId == employeeId 
                    && w.WorkDate >= startDate.Date 
                    && w.WorkDate <= endDate.Date
                    && w.PaymentPeriodId == null)
                .ToListAsync(cancellationToken);

            if (!workLogs.Any())
                throw new InvalidOperationException("Não há registros de horas para o período especificado.");

            var totalHours = workLogs.Sum(w => w.HoursWorked);
            var totalAmount = workLogs.Sum(w => w.TotalAmount);

            var paymentPeriod = new PaymentPeriod
            {
                EmployeeId = employeeId,
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                TotalHours = totalHours,
                TotalAmount = totalAmount,
                Status = PaymentStatus.Pendente,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<PaymentPeriod>().AddAsync(paymentPeriod, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Associar os work logs ao período
            foreach (var workLog in workLogs)
            {
                workLog.PaymentPeriodId = paymentPeriod.Id;
                _unitOfWork.Repository<WorkLog>().Update(workLog);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await MapToDtoAsync(paymentPeriod, cancellationToken);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var paymentPeriod = await _unitOfWork.Repository<PaymentPeriod>().GetByIdAsync(id, cancellationToken);
            if (paymentPeriod == null)
                return false;

            // Verificar se há pagamento
            var payments = await _unitOfWork.Repository<Payment>()
                .FindAsync(p => p.PaymentPeriodId == id, cancellationToken);

            if (payments.Any())
                throw new InvalidOperationException("Não é possível excluir um período que já possui pagamento registrado.");

            // Desassociar work logs
            var workLogs = await _unitOfWork.Context.WorkLogs
                .Where(w => w.PaymentPeriodId == id)
                .ToListAsync(cancellationToken);

            foreach (var workLog in workLogs)
            {
                workLog.PaymentPeriodId = null;
                _unitOfWork.Repository<WorkLog>().Update(workLog);
            }

            _unitOfWork.Repository<PaymentPeriod>().Delete(paymentPeriod);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<PaymentPeriodDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var paymentPeriod = await _unitOfWork.Context.PaymentPeriods
                .Include(p => p.Employee)
                .Include(p => p.WorkLogs)
                .Include(p => p.Payment)
                    .ThenInclude(pay => pay!.PaidByUser)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            return paymentPeriod == null ? null : await MapToDtoAsync(paymentPeriod, cancellationToken);
        }

        public async Task<IEnumerable<PaymentPeriodDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
        {
            var paymentPeriods = await _unitOfWork.Context.PaymentPeriods
                .Include(p => p.Employee)
                .Include(p => p.WorkLogs)
                .Include(p => p.Payment)
                    .ThenInclude(pay => pay!.PaidByUser)
                .Where(p => p.EmployeeId == employeeId)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync(cancellationToken);

            var dtos = new List<PaymentPeriodDto>();
            foreach (var period in paymentPeriods)
            {
                dtos.Add(await MapToDtoAsync(period, cancellationToken));
            }

            return dtos;
        }

        public async Task<IEnumerable<PaymentPeriodSummaryDto>> GetByStatusAsync(PaymentStatus status, CancellationToken cancellationToken = default)
        {
            var paymentPeriods = await _unitOfWork.Context.PaymentPeriods
                .Include(p => p.Employee)
                .Where(p => p.Status == status)
                .OrderBy(p => p.StartDate)
                .Select(p => new PaymentPeriodSummaryDto(
                    p.Id,
                    p.EmployeeId,
                    p.Employee.FullName,
                    p.StartDate,
                    p.EndDate,
                    p.TotalAmount,
                    p.Status
                ))
                .ToListAsync(cancellationToken);

            return paymentPeriods;
        }

        public async Task<PaymentPeriodDto> UpdateStatusAsync(Guid id, PaymentStatus status, CancellationToken cancellationToken = default)
        {
            var paymentPeriod = await _unitOfWork.Repository<PaymentPeriod>().GetByIdAsync(id, cancellationToken);
            if (paymentPeriod == null)
                throw new KeyNotFoundException("Período de pagamento não encontrado.");

            paymentPeriod.Status = status;
            paymentPeriod.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<PaymentPeriod>().Update(paymentPeriod);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await MapToDtoAsync(paymentPeriod, cancellationToken);
        }

        private async Task<PaymentPeriodDto> MapToDtoAsync(PaymentPeriod paymentPeriod, CancellationToken cancellationToken)
        {
            if (paymentPeriod.Employee == null)
            {
                var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(paymentPeriod.EmployeeId, cancellationToken);
                paymentPeriod.Employee = employee!;
            }

            if (paymentPeriod.WorkLogs == null || !paymentPeriod.WorkLogs.Any())
            {
                paymentPeriod.WorkLogs = await _unitOfWork.Context.WorkLogs
                    .Where(w => w.PaymentPeriodId == paymentPeriod.Id)
                    .ToListAsync(cancellationToken);
            }

            var workLogDtos = paymentPeriod.WorkLogs.Select(w => new WorkLogDto(
                w.Id,
                w.EmployeeId,
                paymentPeriod.Employee.FullName,
                w.WorkDate,
                w.HoursWorked,
                w.HourlyRateAtTime,
                w.TotalAmount,
                w.Notes,
                w.PaymentPeriodId,
                w.CreatedAt
            )).ToList();

            PaymentDto? paymentDto = null;
            if (paymentPeriod.Payment != null)
            {
                if (paymentPeriod.Payment.PaidByUser == null)
                {
                    var user = await _unitOfWork.Context.Users.FindAsync(new object[] { paymentPeriod.Payment.PaidByUserId }, cancellationToken);
                    paymentPeriod.Payment.PaidByUser = user!;
                }

                paymentDto = new PaymentDto(
                    paymentPeriod.Payment.Id,
                    paymentPeriod.Payment.EmployeeId,
                    paymentPeriod.Employee.FullName,
                    paymentPeriod.Payment.PaymentPeriodId,
                    paymentPeriod.Payment.PaymentDate,
                    paymentPeriod.Payment.Amount,
                    paymentPeriod.Payment.PaymentMethod,
                    paymentPeriod.Payment.PaymentProof,
                    paymentPeriod.Payment.Notes,
                    paymentPeriod.Payment.PaidByUserId,
                    paymentPeriod.Payment.PaidByUser.FullName ?? paymentPeriod.Payment.PaidByUser.UserName ?? "Desconhecido",
                    paymentPeriod.Payment.CreatedAt
                );
            }

            return new PaymentPeriodDto(
                paymentPeriod.Id,
                paymentPeriod.EmployeeId,
                paymentPeriod.Employee.FullName,
                paymentPeriod.StartDate,
                paymentPeriod.EndDate,
                paymentPeriod.TotalHours,
                paymentPeriod.TotalAmount,
                paymentPeriod.Status,
                paymentDto,
                workLogDtos,
                paymentPeriod.CreatedAt
            );
        }
    }
}
