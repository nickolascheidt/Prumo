using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Application.DTOs.HR;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Data;

namespace SaaS_BasePlatform.Application.Services
{
    public class EmployeeService : IEmployeeService
    {
        private readonly ApplicationDbContext _db;

        public EmployeeService(ApplicationDbContext db) => _db = db;

        public async Task<IReadOnlyList<EmployeeDto>> ListAsync(
            Guid tenantId, bool includeInactive = false, CancellationToken ct = default)
        {
            var query = _db.Employees.IgnoreQueryFilters().Where(e => e.TenantId == tenantId);
            if (!includeInactive) query = query.Where(e => e.IsActive);
            return await query.OrderBy(e => e.FullName).Select(e => ToDto(e)).ToListAsync(ct);
        }

        public async Task<EmployeeDto?> GetAsync(
            Guid tenantId, Guid employeeId, CancellationToken ct = default)
        {
            var e = await _db.Employees.IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == employeeId, ct);
            return e == null ? null : ToDto(e);
        }

        public async Task<EmployeeDto> CreateAsync(
            Guid tenantId, CreateEmployeeRequestDto request, CancellationToken ct = default)
        {
            var cpf = (request.CPF ?? string.Empty).Trim();
            var fullName = (request.FullName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(cpf)) throw new ArgumentException("CPF is required.");
            if (string.IsNullOrEmpty(fullName)) throw new ArgumentException("FullName is required.");
            if (request.HourlyRate <= 0) throw new ArgumentException("HourlyRate must be greater than zero.");
            if (!Enum.IsDefined(typeof(ContractType), request.ContractType))
                throw new ArgumentException($"Invalid ContractType: {request.ContractType}.");
            if (!Enum.IsDefined(typeof(HrPaymentMethod), request.PreferredPaymentMethod))
                throw new ArgumentException($"Invalid PaymentMethod: {request.PreferredPaymentMethod}.");

            var cpfTaken = await _db.Employees.IgnoreQueryFilters()
                .AnyAsync(e => e.TenantId == tenantId && e.CPF == cpf, ct);
            if (cpfTaken)
                throw new InvalidOperationException($"An employee with CPF '{cpf}' already exists.");

            var employee = new Employee
            {
                TenantId                = tenantId,
                FullName                = fullName,
                CPF                     = cpf,
                Phone                   = request.Phone?.Trim(),
                Email                   = request.Email?.Trim(),
                HireDate                = DateTime.SpecifyKind(request.HireDate, DateTimeKind.Utc),
                ContractType            = (ContractType)request.ContractType,
                HourlyRate              = request.HourlyRate,
                PreferredPaymentMethod  = (HrPaymentMethod)request.PreferredPaymentMethod,
                PixKey                  = request.PixKey?.Trim(),
                BankName                = request.BankName?.Trim(),
                BankAccountNumber       = request.BankAccountNumber?.Trim(),
                BankAgency              = request.BankAgency?.Trim(),
                HasSignedContract       = request.HasSignedContract,
                ApplicationUserId       = request.ApplicationUserId
            };

            _db.Employees.Add(employee);
            await _db.SaveChangesAsync(ct);
            return ToDto(employee);
        }

        public async Task<EmployeeDto> UpdateAsync(
            Guid tenantId, Guid employeeId, UpdateEmployeeRequestDto request, CancellationToken ct = default)
        {
            var employee = await _db.Employees.IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == employeeId, ct)
                ?? throw new KeyNotFoundException("Employee not found.");

            var fullName = (request.FullName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(fullName)) throw new ArgumentException("FullName is required.");
            if (request.HourlyRate <= 0) throw new ArgumentException("HourlyRate must be greater than zero.");
            if (!Enum.IsDefined(typeof(ContractType), request.ContractType))
                throw new ArgumentException($"Invalid ContractType: {request.ContractType}.");
            if (!Enum.IsDefined(typeof(HrPaymentMethod), request.PreferredPaymentMethod))
                throw new ArgumentException($"Invalid PaymentMethod: {request.PreferredPaymentMethod}.");

            employee.FullName               = fullName;
            employee.Phone                  = request.Phone?.Trim();
            employee.Email                  = request.Email?.Trim();
            employee.IsActive               = request.IsActive;
            employee.ContractType           = (ContractType)request.ContractType;
            employee.HourlyRate             = request.HourlyRate;
            employee.PreferredPaymentMethod = (HrPaymentMethod)request.PreferredPaymentMethod;
            employee.PixKey                 = request.PixKey?.Trim();
            employee.BankName               = request.BankName?.Trim();
            employee.BankAccountNumber      = request.BankAccountNumber?.Trim();
            employee.BankAgency             = request.BankAgency?.Trim();
            employee.HasSignedContract      = request.HasSignedContract;
            employee.TerminationDate        = request.TerminationDate.HasValue
                ? DateTime.SpecifyKind(request.TerminationDate.Value, DateTimeKind.Utc)
                : null;

            await _db.SaveChangesAsync(ct);
            return ToDto(employee);
        }

        public async Task DeactivateAsync(
            Guid tenantId, Guid employeeId, CancellationToken ct = default)
        {
            var employee = await _db.Employees.IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == employeeId, ct)
                ?? throw new KeyNotFoundException("Employee not found.");
            employee.IsActive = false;
            await _db.SaveChangesAsync(ct);
        }

        private static EmployeeDto ToDto(Employee e) => new(
            e.Id, e.TenantId, e.FullName, e.CPF, e.Phone, e.Email,
            e.HireDate, e.TerminationDate, e.IsActive,
            (int)e.ContractType, e.ContractType.ToString(),
            e.HourlyRate,
            (int)e.PreferredPaymentMethod, e.PreferredPaymentMethod.ToString(),
            e.PixKey, e.BankName, e.BankAccountNumber, e.BankAgency,
            e.HasSignedContract, e.ContractSignedDate,
            e.ApplicationUserId, e.CreatedAt, e.UpdatedAt
        );
    }
}
