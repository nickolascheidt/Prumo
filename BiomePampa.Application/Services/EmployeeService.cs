using BiomePampa.Application.DTOs.Employee;
using BiomePampa.Domain.Entities;
using BiomePampa.Infrastructure.Repositories;
using FluentValidation;

namespace BiomePampa.Application.Services
{
    public class EmployeeService : IEmployeeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IValidator<CreateEmployeeDto> _createValidator;
        private readonly IValidator<UpdateEmployeeDto> _updateValidator;

        public EmployeeService(
            IUnitOfWork unitOfWork,
            IValidator<CreateEmployeeDto> createValidator,
            IValidator<UpdateEmployeeDto> updateValidator)
        {
            _unitOfWork = unitOfWork;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
        }

        public async Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto, CancellationToken cancellationToken = default)
        {
            // Validar DTO
            await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

            // Verificar se CPF já existe
            var existingEmployees = await _unitOfWork.Repository<Employee>()
                .FindAsync(e => e.CPF == dto.CPF, cancellationToken);

            if (existingEmployees.Any())
                throw new InvalidOperationException("Já existe um funcionário cadastrado com este CPF.");

            var employee = new Employee
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
                ApplicationUserId = dto.ApplicationUserId
            };

            await _unitOfWork.Repository<Employee>().AddAsync(employee, cancellationToken);

            return MapToDto(employee);
        }

        public async Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeDto dto, CancellationToken cancellationToken = default)
        {
            // Validar DTO
            await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

            var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(id, cancellationToken);
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

            await _unitOfWork.Repository<Employee>().UpdateAsync(employee, cancellationToken);

            return MapToDto(employee);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.Repository<Employee>().DeleteAsync(id, cancellationToken);
            return true;
        }

        public async Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(id, cancellationToken);
            return employee == null ? null : MapToDto(employee);
        }

        public async Task<EmployeeDto?> GetByCPFAsync(string cpf, CancellationToken cancellationToken = default)
        {
            var employees = await _unitOfWork.Repository<Employee>()
                .FindAsync(e => e.CPF == cpf, cancellationToken);

            var employee = employees.FirstOrDefault();
            return employee == null ? null : MapToDto(employee);
        }

        public async Task<IEnumerable<EmployeeDto>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var employees = await _unitOfWork.Repository<Employee>().GetAllAsync(cancellationToken);

            if (!includeInactive)
                employees = employees.Where(e => e.IsActive).ToList();

            return employees.OrderBy(e => e.FullName).Select(MapToDto);
        }

        public async Task<IEnumerable<EmployeeSummaryDto>> GetSummariesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var employees = await _unitOfWork.Repository<Employee>().GetAllAsync(cancellationToken);

            if (!includeInactive)
                employees = employees.Where(e => e.IsActive).ToList();

            return employees
                .OrderBy(e => e.FullName)
                .Select(e => new EmployeeSummaryDto(
                    e.Id,
                    e.FullName,
                    e.CPF,
                    e.IsActive,
                    e.ContractType,
                    e.HourlyRate
                ));
        }

        private static EmployeeDto MapToDto(Employee employee)
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
