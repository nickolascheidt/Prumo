using BiomePampa.Application.DTOs.Employee;
using BiomePampa.Domain.Entities;
using BiomePampa.Domain.Enums;
using BiomePampa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BiomePampa.Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _context;

        public PaymentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaymentDto> CreateAsync(CreatePaymentDto dto, Guid paidByUserId, CancellationToken cancellationToken = default)
        {
            var paymentPeriod = await _context.PaymentPeriods
                .Include(p => p.Employee)
                .Include(p => p.Payment)
                .FirstOrDefaultAsync(p => p.Id == dto.PaymentPeriodId, cancellationToken);

            if (paymentPeriod == null)
                throw new KeyNotFoundException("Período de pagamento não encontrado.");

            if (paymentPeriod.Payment != null)
                throw new InvalidOperationException("Este período já possui um pagamento registrado.");

            var user = await _context.Users.FindAsync(new object[] { paidByUserId }, cancellationToken);
            if (user == null)
                throw new KeyNotFoundException("Usuário não encontrado.");

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

            _context.Payments.Add(payment);

            // Atualizar status do período
            paymentPeriod.Status = PaymentStatus.Pago;
            paymentPeriod.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            payment.PaidByUser = user;
            payment.Employee = paymentPeriod.Employee;
            payment.PaymentPeriod = paymentPeriod;

            return MapToDto(payment);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var payment = await _context.Payments
                .Include(p => p.PaymentPeriod)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (payment == null)
                return false;

            // Reverter status do período
            payment.PaymentPeriod.Status = PaymentStatus.Pendente;
            payment.PaymentPeriod.UpdatedAt = DateTime.UtcNow;

            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<PaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var payment = await _context.Payments
                .Include(p => p.Employee)
                .Include(p => p.PaidByUser)
                .Include(p => p.PaymentPeriod)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            return payment == null ? null : MapToDto(payment);
        }

        public async Task<IEnumerable<PaymentDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
        {
            var payments = await _context.Payments
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
            var payments = await _context.Payments
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
            var payments = await _context.Payments
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
