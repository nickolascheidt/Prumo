using BiomePampa.Application.DTOs.Employee;
using BiomePampa.Domain.Entities;
using BiomePampa.Domain.Enums;
using BiomePampa.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BiomePampa.Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PaymentService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PaymentDto> CreateAsync(CreatePaymentDto dto, Guid paidByUserId, CancellationToken cancellationToken = default)
        {
            // Buscar período de pagamento
            var paymentPeriods = await _unitOfWork.Repository<PaymentPeriod>()
                .FindAsync(p => p.Id == dto.PaymentPeriodId, cancellationToken);

            var paymentPeriod = paymentPeriods.FirstOrDefault();
            if (paymentPeriod == null)
                throw new KeyNotFoundException("Período de pagamento não encontrado.");

            // Verificar se já existe pagamento para este período
            var existingPayments = await _unitOfWork.Repository<Payment>()
                .FindAsync(p => p.PaymentPeriodId == dto.PaymentPeriodId, cancellationToken);

            if (existingPayments.Any())
                throw new InvalidOperationException("Este período já possui um pagamento registrado.");

            // Buscar usuário (ApplicationUser não herda de EntityBase)
            var user = await _unitOfWork.Context.Users.FindAsync(new object[] { paidByUserId }, cancellationToken);
            if (user == null)
                throw new KeyNotFoundException("Usuário não encontrado.");

            // Buscar funcionário
            var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(paymentPeriod.EmployeeId, cancellationToken);
            if (employee == null)
                throw new KeyNotFoundException("Funcionário não encontrado.");

            var payment = new Payment
            {
                EmployeeId = paymentPeriod.EmployeeId,
                PaymentPeriodId = paymentPeriod.Id,
                PaymentDate = dto.PaymentDate,
                Amount = paymentPeriod.TotalAmount,
                PaymentMethod = dto.PaymentMethod,
                PaymentProof = dto.PaymentProof,
                Notes = dto.Notes,
                PaidByUserId = paidByUserId,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<Payment>().AddAsync(payment, cancellationToken);

            // Atualizar status do período
            paymentPeriod.Status = PaymentStatus.Pago;
            paymentPeriod.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<PaymentPeriod>().Update(paymentPeriod);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Carregar entidades para retorno
            payment.PaidByUser = user;
            payment.Employee = employee;
            payment.PaymentPeriod = paymentPeriod;

            return MapToDto(payment);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Repository<Payment>().GetByIdAsync(id, cancellationToken);
            if (payment == null)
                return false;

            // Buscar período de pagamento
            var paymentPeriod = await _unitOfWork.Repository<PaymentPeriod>().GetByIdAsync(payment.PaymentPeriodId, cancellationToken);
            if (paymentPeriod != null)
            {
                // Reverter status do período
                paymentPeriod.Status = PaymentStatus.Pendente;
                paymentPeriod.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Repository<PaymentPeriod>().Update(paymentPeriod);
            }

            _unitOfWork.Repository<Payment>().Delete(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<PaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var payment = await _unitOfWork.Context.Payments
                .Include(p => p.Employee)
                .Include(p => p.PaidByUser)
                .Include(p => p.PaymentPeriod)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            return payment == null ? null : MapToDto(payment);
        }

        public async Task<IEnumerable<PaymentDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
        {
            var payments = await _unitOfWork.Context.Payments
                .Include(p => p.Employee)
                .Include(p => p.PaidByUser)
                .Include(p => p.PaymentPeriod)
                .Where(p => p.EmployeeId == employeeId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync(cancellationToken);

            return payments.Select(MapToDto);
        }

        public async Task<IEnumerable<PaymentDto>> GetByPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var payments = await _unitOfWork.Context.Payments
                .Include(p => p.Employee)
                .Include(p => p.PaidByUser)
                .Include(p => p.PaymentPeriod)
                .Where(p => p.PaymentDate >= startDate.Date && p.PaymentDate <= endDate.Date)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync(cancellationToken);

            return payments.Select(MapToDto);
        }

        public async Task<IEnumerable<PaymentSummaryDto>> GetRecentPaymentsAsync(int count = 10, CancellationToken cancellationToken = default)
        {
            var payments = await _unitOfWork.Context.Payments
                .Include(p => p.Employee)
                .Include(p => p.PaidByUser)
                .OrderByDescending(p => p.PaymentDate)
                .Take(count)
                .Select(p => new PaymentSummaryDto(
                    p.Id,
                    p.Employee.FullName,
                    p.PaymentDate,
                    p.Amount,
                    p.PaymentMethod,
                    p.PaidByUser.FullName ?? p.PaidByUser.UserName ?? "Desconhecido"
                ))
                .ToListAsync(cancellationToken);

            return payments;
        }

        private static PaymentDto MapToDto(Payment payment)
        {
            return new PaymentDto(
                payment.Id,
                payment.EmployeeId,
                payment.Employee.FullName,
                payment.PaymentPeriodId,
                payment.PaymentDate,
                payment.Amount,
                payment.PaymentMethod,
                payment.PaymentProof,
                payment.Notes,
                payment.PaidByUserId,
                payment.PaidByUser.FullName ?? payment.PaidByUser.UserName ?? "Desconhecido",
                payment.CreatedAt
            );
        }
    }
}
