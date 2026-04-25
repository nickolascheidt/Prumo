using SaaS_BasePlatform.Application.DTOs.Employee;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Repositories;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using System.Linq.Expressions;

namespace SaaS_BasePlatform.Tests.Services;

public class EmployeeServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Employee> _employeeRepository;
    private readonly IValidator<CreateEmployeeDto> _createValidator;
    private readonly IValidator<UpdateEmployeeDto> _updateValidator;
    private readonly EmployeeService _service;

    public EmployeeServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _employeeRepository = Substitute.For<IRepository<Employee>>();
        _createValidator = Substitute.For<IValidator<CreateEmployeeDto>>();
        _updateValidator = Substitute.For<IValidator<UpdateEmployeeDto>>();

        _unitOfWork.Repository<Employee>().Returns(_employeeRepository);

        _createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateEmployeeDto>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateEmployeeDto>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _service = new EmployeeService(_unitOfWork, _createValidator, _updateValidator);
    }

    private static CreateEmployeeDto CreateValidDto() => new(
        FullName: "João da Silva",
        CPF: "52998224725",
        Phone: "51999998888",
        Email: "joao@email.com",
        HireDate: DateTime.Today.AddMonths(-1),
        ContractType: ContractType.CLT,
        HourlyRate: 25.00m,
        PreferredPaymentMethod: PaymentMethod.Pix,
        PixKey: "joao@email.com",
        BankName: null,
        BankAccountNumber: null,
        BankAgency: null,
        HasSignedContract: true,
        ApplicationUserId: null
    );

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_WithValidDto_ShouldReturnEmployeeDto()
    {
        var dto = CreateValidDto();
        _employeeRepository
            .FindAsync(Arg.Any<Expression<Func<Employee, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Employee>());
        _employeeRepository
            .AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Employee>());

        var result = await _service.CreateAsync(dto);

        Assert.Equal(dto.FullName, result.FullName);
        Assert.Equal(dto.CPF, result.CPF);
        Assert.Equal(dto.HourlyRate, result.HourlyRate);
        Assert.True(result.IsActive);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateCPF_ShouldThrowInvalidOperationException()
    {
        var dto = CreateValidDto();
        var existing = new Employee { CPF = dto.CPF, FullName = "Outro" };
        _employeeRepository
            .FindAsync(Arg.Any<Expression<Func<Employee, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { existing });

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_WithSignedContract_ShouldSetContractSignedDate()
    {
        var dto = CreateValidDto() with { HasSignedContract = true };
        _employeeRepository
            .FindAsync(Arg.Any<Expression<Func<Employee, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Employee>());
        _employeeRepository
            .AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Employee>());

        var result = await _service.CreateAsync(dto);

        Assert.NotNull(result.ContractSignedDate);
    }

    [Fact]
    public async Task CreateAsync_WithoutSignedContract_ShouldNotSetContractSignedDate()
    {
        var dto = CreateValidDto() with { HasSignedContract = false };
        _employeeRepository
            .FindAsync(Arg.Any<Expression<Func<Employee, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Employee>());
        _employeeRepository
            .AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Employee>());

        var result = await _service.CreateAsync(dto);

        Assert.Null(result.ContractSignedDate);
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_WhenEmployeeExists_ShouldReturnDto()
    {
        var employee = new Employee
        {
            FullName = "João",
            CPF = "52998224725",
            HireDate = DateTime.Today,
            ContractType = ContractType.CLT,
            HourlyRate = 20m,
            PreferredPaymentMethod = PaymentMethod.Dinheiro
        };
        _employeeRepository.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);

        var result = await _service.GetByIdAsync(employee.Id);

        Assert.NotNull(result);
        Assert.Equal(employee.FullName, result.FullName);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ShouldReturnNull()
    {
        var id = Guid.NewGuid();
        _employeeRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Employee?)null);

        var result = await _service.GetByIdAsync(id);

        Assert.Null(result);
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        var id = Guid.NewGuid();
        _employeeRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Employee?)null);
        var dto = new UpdateEmployeeDto("Nome", null, null, true, ContractType.CLT, 20m, PaymentMethod.Dinheiro, null, null, null, null, false, null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateAsync(id, dto));
    }

    [Fact]
    public async Task UpdateAsync_WhenFound_ShouldUpdateAndReturnDto()
    {
        var employee = new Employee
        {
            FullName = "Antigo",
            CPF = "52998224725",
            HireDate = DateTime.Today,
            ContractType = ContractType.CLT,
            HourlyRate = 20m,
            PreferredPaymentMethod = PaymentMethod.Dinheiro
        };
        _employeeRepository.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
        var dto = new UpdateEmployeeDto("Novo Nome", "51999990000", null, true, ContractType.Frio, 30m, PaymentMethod.Dinheiro, null, null, null, null, true, null);

        var result = await _service.UpdateAsync(employee.Id, dto);

        Assert.Equal("Novo Nome", result.FullName);
        Assert.Equal(30m, result.HourlyRate);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_WhenExists_ShouldReturnTrueAndSave()
    {
        var id = Guid.NewGuid();
        _employeeRepository.DeleteAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.DeleteAsync(id);

        Assert.True(result);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ShouldReturnFalseAndNotSave()
    {
        var id = Guid.NewGuid();
        _employeeRepository.DeleteAsync(id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _service.DeleteAsync(id);

        Assert.False(result);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_WhenNotIncludingInactive_ShouldFilterInactive()
    {
        var employees = new List<Employee>
        {
            new() { FullName = "Ativo", CPF = "111", IsActive = true, PreferredPaymentMethod = PaymentMethod.Dinheiro },
            new() { FullName = "Inativo", CPF = "222", IsActive = false, PreferredPaymentMethod = PaymentMethod.Dinheiro }
        };
        _employeeRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(employees);

        var result = (await _service.GetAllAsync(includeInactive: false)).ToList();

        Assert.Single(result);
        Assert.Equal("Ativo", result[0].FullName);
    }

    [Fact]
    public async Task GetAllAsync_WhenIncludingInactive_ShouldReturnAll()
    {
        var employees = new List<Employee>
        {
            new() { FullName = "Ativo", CPF = "111", IsActive = true, PreferredPaymentMethod = PaymentMethod.Dinheiro },
            new() { FullName = "Inativo", CPF = "222", IsActive = false, PreferredPaymentMethod = PaymentMethod.Dinheiro }
        };
        _employeeRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(employees);

        var result = (await _service.GetAllAsync(includeInactive: true)).ToList();

        Assert.Equal(2, result.Count);
    }

    #endregion
}
