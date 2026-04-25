using SaaS_BasePlatform.Application.DTOs.Employee;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Repositories;
using NSubstitute;
using System.Linq.Expressions;

namespace SaaS_BasePlatform.Tests.Services;

public class WorkLogServiceTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Employee> _employeeRepository;
    private readonly IRepository<WorkLog> _workLogRepository;
    private readonly WorkLogService _service;

    public WorkLogServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _employeeRepository = Substitute.For<IRepository<Employee>>();
        _workLogRepository = Substitute.For<IRepository<WorkLog>>();

        _unitOfWork.Repository<Employee>().Returns(_employeeRepository);
        _unitOfWork.Repository<WorkLog>().Returns(_workLogRepository);

        _service = new WorkLogService(_unitOfWork);
    }

    private static Employee CreateTestEmployee() => new()
    {
        FullName = "João da Silva",
        CPF = "52998224725",
        HireDate = DateTime.Today.AddMonths(-6),
        ContractType = ContractType.CLT,
        HourlyRate = 25.00m,
        PreferredPaymentMethod = PaymentMethod.Dinheiro
    };

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_WithValidDto_ShouldCalculateTotalAmount()
    {
        var employee = CreateTestEmployee();
        var dto = new CreateWorkLogDto(employee.Id, DateTime.Today, HoursWorked: 8m, Notes: null);

        _employeeRepository.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
        _workLogRepository
            .FindAsync(Arg.Any<Expression<Func<WorkLog, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<WorkLog>());
        _workLogRepository
            .AddAsync(Arg.Any<WorkLog>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<WorkLog>());

        var result = await _service.CreateAsync(dto);

        Assert.Equal(200m, result.TotalAmount); // 8h * 25.00
        Assert.Equal(25.00m, result.HourlyRateAtTime);
        Assert.Equal(employee.FullName, result.EmployeeName);
    }

    [Fact]
    public async Task CreateAsync_WhenEmployeeNotFound_ShouldThrowKeyNotFoundException()
    {
        var dto = new CreateWorkLogDto(Guid.NewGuid(), DateTime.Today, 8m, null);
        _employeeRepository.GetByIdAsync(dto.EmployeeId, Arg.Any<CancellationToken>()).Returns((Employee?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_WhenDuplicateDate_ShouldThrowInvalidOperationException()
    {
        var employee = CreateTestEmployee();
        var dto = new CreateWorkLogDto(employee.Id, DateTime.Today, 8m, null);

        _employeeRepository.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
        _workLogRepository
            .FindAsync(Arg.Any<Expression<Func<WorkLog, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new WorkLog { EmployeeId = employee.Id, WorkDate = DateTime.Today } });

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_ShouldUseEmployeeHourlyRate()
    {
        var employee = CreateTestEmployee();
        employee.HourlyRate = 50m;
        var dto = new CreateWorkLogDto(employee.Id, DateTime.Today, 4m, null);

        _employeeRepository.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);
        _workLogRepository
            .FindAsync(Arg.Any<Expression<Func<WorkLog, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<WorkLog>());
        _workLogRepository
            .AddAsync(Arg.Any<WorkLog>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<WorkLog>());

        var result = await _service.CreateAsync(dto);

        Assert.Equal(50m, result.HourlyRateAtTime);
        Assert.Equal(200m, result.TotalAmount); // 4h * 50.00
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ShouldThrowKeyNotFoundException()
    {
        var id = Guid.NewGuid();
        _workLogRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((WorkLog?)null);
        var dto = new UpdateWorkLogDto(DateTime.Today, 6m, null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateAsync(id, dto));
    }

    [Fact]
    public async Task UpdateAsync_WhenLinkedToPaymentPeriod_ShouldThrowInvalidOperationException()
    {
        var workLog = new WorkLog
        {
            EmployeeId = Guid.NewGuid(),
            WorkDate = DateTime.Today,
            HoursWorked = 8m,
            HourlyRateAtTime = 25m,
            TotalAmount = 200m,
            PaymentPeriodId = Guid.NewGuid()
        };
        _workLogRepository.GetByIdAsync(workLog.Id, Arg.Any<CancellationToken>()).Returns(workLog);
        var dto = new UpdateWorkLogDto(DateTime.Today, 6m, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateAsync(workLog.Id, dto));
    }

    [Fact]
    public async Task UpdateAsync_WhenValid_ShouldRecalculateTotal()
    {
        var employee = CreateTestEmployee();
        var workLog = new WorkLog
        {
            EmployeeId = employee.Id,
            Employee = employee,
            WorkDate = DateTime.Today,
            HoursWorked = 8m,
            HourlyRateAtTime = 25m,
            TotalAmount = 200m,
            PaymentPeriodId = null
        };
        _workLogRepository.GetByIdAsync(workLog.Id, Arg.Any<CancellationToken>()).Returns(workLog);
        _employeeRepository.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);

        var dto = new UpdateWorkLogDto(DateTime.Today, 6m, "Saiu mais cedo");

        var result = await _service.UpdateAsync(workLog.Id, dto);

        Assert.Equal(150m, result.TotalAmount); // 6h * 25.00
        Assert.Equal("Saiu mais cedo", result.Notes);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ShouldReturnFalse()
    {
        var id = Guid.NewGuid();
        _workLogRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((WorkLog?)null);

        var result = await _service.DeleteAsync(id);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_WhenLinkedToPaymentPeriod_ShouldThrowInvalidOperationException()
    {
        var workLog = new WorkLog
        {
            EmployeeId = Guid.NewGuid(),
            WorkDate = DateTime.Today,
            HoursWorked = 8m,
            HourlyRateAtTime = 25m,
            TotalAmount = 200m,
            PaymentPeriodId = Guid.NewGuid()
        };
        _workLogRepository.GetByIdAsync(workLog.Id, Arg.Any<CancellationToken>()).Returns(workLog);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteAsync(workLog.Id));
    }

    [Fact]
    public async Task DeleteAsync_WhenValidAndUnlinked_ShouldReturnTrue()
    {
        var workLog = new WorkLog
        {
            EmployeeId = Guid.NewGuid(),
            WorkDate = DateTime.Today,
            HoursWorked = 8m,
            HourlyRateAtTime = 25m,
            TotalAmount = 200m,
            PaymentPeriodId = null
        };
        _workLogRepository.GetByIdAsync(workLog.Id, Arg.Any<CancellationToken>()).Returns(workLog);

        var result = await _service.DeleteAsync(workLog.Id);

        Assert.True(result);
        _workLogRepository.Received(1).Delete(workLog);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion
}
