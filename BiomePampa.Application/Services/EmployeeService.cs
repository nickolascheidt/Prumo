using BiomePampa.Application.DTOs.Employee;
using BiomePampa.Domain.Entities;
using BiomePampa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BiomePampa.Application.Services
{
    public class EmployeeService : IEmployeeService
    {
        private readonly ApplicationDbContext _context;

        public EmployeeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto, CancellationToken cancellationToken = default)
        {
            // Verificar se CPF já existe
            var existingEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.CPF == dto.CPF, cancellationToken);

            if (existingEmployee != null)
                throw new InvalidOperationException("Já existe um funcionário cadastrado com este CPF.");

            var employee = new Domain.Entities.Employee
            {
                FullName = dto.FullName,
                CPF = dto.CPF,
                Phone = dto.Phone,
                Email = dto.Email,
                HireDate = dto.HireDate,
                IsActive = true,
                ContractType = dto.ContractType,
                HourlyRate = dto.HourlyRate,
                PreferredPaymentMethod = dto.PreferredPaymentMethod,
                PixKey = dto.PixKey,
                BankName = dto.BankName,
                BankAccountNumber = dto.BankAccountNumber,
                BankAgency = dto.BankAgency,
                HasSignedContract = dto.HasSignedContract,
                ContractSignedDate = dto.HasSignedContract ? DateTime.UtcNow : null,
                ApplicationUserId = dto.ApplicationUserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync(cancellationToken);

            return MapToDto(employee);
        }

        public async Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeDto dto, CancellationToken cancellationToken = default)
        {
            var employee = await _context.Employees.FindAsync(new object[] { id }, cancellationToken);
            if (employee == null)
                throw new KeyNotFoundException("Funcionário não encontrado.");

            employee.FullName = dto.FullName;
            employee.Phone = dto.Phone;
            employee.Email = dto.Email;
            employee.IsActive = dto.IsActive;
            employee.ContractType = dto.ContractType;
            employee.HourlyRate = dto.HourlyRate;
            employee.PreferredPaymentMethod = dto.PreferredPaymentMethod;
            employee.PixKey = dto.PixKey;
            employee.BankName = dto.BankName;
            employee.BankAccountNumber = dto.BankAccountNumber;
            employee.BankAgency = dto.BankAgency;
            employee.HasSignedContract = dto.HasSignedContract;
            employee.TerminationDate = dto.TerminationDate;
            employee.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return MapToDto(employee);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var employee = await _context.Employees.FindAsync(new object[] { id }, cancellationToken);
            if (employee == null)
                return false;

            // Soft delete - apenas marcar como inativo
            employee.IsActive = false;
            employee.TerminationDate = DateTime.UtcNow;
            employee.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

            return employee == null ? null : MapToDto(employee);
        }

        public async Task<EmployeeDto?> GetByCPFAsync(string cpf, CancellationToken cancellationToken = default)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.CPF == cpf, cancellationToken);

            return employee == null ? null : MapToDto(employee);
        }

        public async Task<IEnumerable<EmployeeDto>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var query = _context.Employees.AsQueryable();

            if (!includeInactive)
                query = query.Where(e => e.IsActive);

            var employees = await query
                .OrderBy(e => e.FullName)
                .ToListAsync(cancellationToken);

            return employees.Select(MapToDto);
        }

        public async Task<IEnumerable<EmployeeSummaryDto>> GetSummariesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var query = _context.Employees.AsQueryable();

            if (!includeInactive)
                query = query.Where(e => e.IsActive);

            var employees = await query
                .OrderBy(e => e.FullName)
                .Select(e => new EmployeeSummaryDto(
                    e.Id,
                    e.FullName,
                    e.CPF,
                    e.IsActive,
                    e.ContractType,
                    e.HourlyRate
                ))
                .ToListAsync(cancellationToken);

            return employees;
        }

        private static EmployeeDto MapToDto(Domain.Entities.Employee employee)
        {
            return new EmployeeDto(
                employee.Id,
                employee.FullName,
                employee.CPF,
                employee.Phone,
                employee.Email,
                employee.HireDate,
                employee.TerminationDate,
                employee.IsActive,
                employee.ContractType,
                employee.HourlyRate,
                employee.PreferredPaymentMethod,
                employee.PixKey,
                employee.BankName,
                employee.BankAccountNumber,
                employee.BankAgency,
                employee.HasSignedContract,
                employee.ContractSignedDate,
                employee.ApplicationUserId,
                employee.CreatedAt,
                employee.UpdatedAt
            );
        }
    }
}
