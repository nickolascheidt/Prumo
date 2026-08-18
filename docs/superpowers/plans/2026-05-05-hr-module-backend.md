# HR Module Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a multi-tenant HR module to the SaaS platform — Employee management, WorkLog (daily hours), PaymentPeriod (aggregation), and Payment (disbursement record) — adapted from the BiomePampa single-tenant design.

**Architecture:** Clean Architecture following the existing patterns. Domain entities (Employee, WorkLog, PaymentPeriod, Payment) implement `EntityBase`; Employee also implements `ITenantScoped` so the global tenant query filter applies automatically. WorkLogs/PaymentPeriods/Payments are accessed through their parent Employee, which is always validated against the tenantId route parameter. Application services use `ApplicationDbContext` directly (same pattern as `AccountService`, `JournalService`). Controllers follow the `TenantRole`-based access check pattern from `ChartOfAccountsController`. Domain permission constants `employees.*`, `worklogs.*`, and `payments.*` already exist in `Domain/Authorization/Permissions.cs`.

**Tech Stack:** .NET 10, EF Core + SQL Server, xUnit + NSubstitute (tests), FluentValidation (already wired), JWT auth (existing pattern)

---

## File Map

### New — Domain
- `SaaS_BasePlatform.Domain/Enums/ContractType.cs`
- `SaaS_BasePlatform.Domain/Enums/HrPaymentMethod.cs`
- `SaaS_BasePlatform.Domain/Enums/PaymentStatus.cs`
- `SaaS_BasePlatform.Domain/Entities/Employee.cs`
- `SaaS_BasePlatform.Domain/Entities/WorkLog.cs` — includes static `CalculateTotalAmount()`
- `SaaS_BasePlatform.Domain/Entities/PaymentPeriod.cs` — includes static `CalculateSummary()`
- `SaaS_BasePlatform.Domain/Entities/Payment.cs`

### New — Application DTOs
- `SaaS_BasePlatform.Application/DTOs/HR/EmployeeDtos.cs`
- `SaaS_BasePlatform.Application/DTOs/HR/WorkLogDtos.cs`
- `SaaS_BasePlatform.Application/DTOs/HR/PaymentPeriodDtos.cs`
- `SaaS_BasePlatform.Application/DTOs/HR/PaymentDtos.cs`

### New — Application Services
- `SaaS_BasePlatform.Application/Services/IEmployeeService.cs`
- `SaaS_BasePlatform.Application/Services/EmployeeService.cs`
- `SaaS_BasePlatform.Application/Services/IWorkLogService.cs`
- `SaaS_BasePlatform.Application/Services/WorkLogService.cs`
- `SaaS_BasePlatform.Application/Services/IPaymentPeriodService.cs`
- `SaaS_BasePlatform.Application/Services/PaymentPeriodService.cs`
- `SaaS_BasePlatform.Application/Services/IPaymentService.cs`
- `SaaS_BasePlatform.Application/Services/PaymentService.cs`

### New — Infrastructure
- `SaaS_BasePlatform.Infrastructure/Data/Configurations/EmployeeConfiguration.cs`
- `SaaS_BasePlatform.Infrastructure/Data/Configurations/WorkLogConfiguration.cs`
- `SaaS_BasePlatform.Infrastructure/Data/Configurations/PaymentPeriodConfiguration.cs`
- `SaaS_BasePlatform.Infrastructure/Data/Configurations/PaymentConfiguration.cs`

### New — API Controllers
- `SaaS_BasePlatform.Api/Controllers/EmployeesController.cs`
- `SaaS_BasePlatform.Api/Controllers/WorkLogsController.cs`
- `SaaS_BasePlatform.Api/Controllers/PaymentPeriodsController.cs`
- `SaaS_BasePlatform.Api/Controllers/PaymentsController.cs`

### New — Tests
- `SaaS_BasePlatform.Tests/Domain/WorkLogTests.cs`
- `SaaS_BasePlatform.Tests/Domain/PaymentPeriodTests.cs`

### Modified
- `SaaS_BasePlatform.Infrastructure/Data/ApplicationDbContext.cs` — add 4 DbSets
- `SaaS_BasePlatform.Api/Configuration/DependencyInjectionConfiguration.cs` — register 4 services

---

## Task 1: Create Feature Branch

**Files:** none

- [ ] **Step 1: Create and switch to feature branch**

```bash
git checkout -b feature/hr-module-backend
```

Expected output: `Switched to a new branch 'feature/hr-module-backend'`

---

## Task 2: Domain Enums

**Files:**
- Create: `SaaS_BasePlatform.Domain/Enums/ContractType.cs`
- Create: `SaaS_BasePlatform.Domain/Enums/HrPaymentMethod.cs`
- Create: `SaaS_BasePlatform.Domain/Enums/PaymentStatus.cs`

> **Note:** The enum is named `HrPaymentMethod` (not `PaymentMethod`) to avoid a naming collision with the existing `PaymentMethod` in the BiomePampa project if it is ever merged.

- [ ] **Step 1: Create ContractType.cs**

```csharp
namespace SaaS_BasePlatform.Domain.Enums
{
    public enum ContractType
    {
        CLT = 1,
        Temporary = 2,
        Daily = 3
    }
}
```

- [ ] **Step 2: Create HrPaymentMethod.cs**

```csharp
namespace SaaS_BasePlatform.Domain.Enums
{
    public enum HrPaymentMethod
    {
        Cash = 1,
        Pix = 2,
        BankTransfer = 3,
        Check = 4
    }
}
```

- [ ] **Step 3: Create PaymentStatus.cs**

```csharp
namespace SaaS_BasePlatform.Domain.Enums
{
    public enum PaymentStatus
    {
        Pending = 1,
        Paid = 2,
        Cancelled = 3,
        Overdue = 4
    }
}
```

- [ ] **Step 4: Build Domain**

```bash
dotnet build SaaS_BasePlatform.Domain
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 5: Commit**

```bash
git add SaaS_BasePlatform.Domain/Enums/ContractType.cs \
        SaaS_BasePlatform.Domain/Enums/HrPaymentMethod.cs \
        SaaS_BasePlatform.Domain/Enums/PaymentStatus.cs
git commit -m "feat: add HR enums (ContractType, HrPaymentMethod, PaymentStatus)"
```

---

## Task 3: Domain Entities + Tests

**Files:**
- Create: `SaaS_BasePlatform.Tests/Domain/WorkLogTests.cs`
- Create: `SaaS_BasePlatform.Tests/Domain/PaymentPeriodTests.cs`
- Create: `SaaS_BasePlatform.Domain/Entities/Employee.cs`
- Create: `SaaS_BasePlatform.Domain/Entities/WorkLog.cs`
- Create: `SaaS_BasePlatform.Domain/Entities/PaymentPeriod.cs`
- Create: `SaaS_BasePlatform.Domain/Entities/Payment.cs`

- [ ] **Step 1: Write failing tests for WorkLog.CalculateTotalAmount**

Create `SaaS_BasePlatform.Tests/Domain/WorkLogTests.cs`:

```csharp
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Tests.Domain;

public class WorkLogTests
{
    [Fact]
    public void CalculateTotalAmount_MultipliesHoursAndRate()
    {
        var result = WorkLog.CalculateTotalAmount(8m, 15.50m);
        Assert.Equal(124.00m, result);
    }

    [Fact]
    public void CalculateTotalAmount_ZeroHours_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => WorkLog.CalculateTotalAmount(0m, 15m));
    }

    [Fact]
    public void CalculateTotalAmount_ZeroRate_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => WorkLog.CalculateTotalAmount(8m, 0m));
    }

    [Fact]
    public void CalculateTotalAmount_FractionalHours_RoundsToTwoDecimalPlaces()
    {
        var result = WorkLog.CalculateTotalAmount(7.5m, 11.33m);
        Assert.Equal(84.98m, result);
    }
}
```

- [ ] **Step 2: Write failing tests for PaymentPeriod.CalculateSummary**

Create `SaaS_BasePlatform.Tests/Domain/PaymentPeriodTests.cs`:

```csharp
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Tests.Domain;

public class PaymentPeriodTests
{
    [Fact]
    public void CalculateSummary_EmptyLogs_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PaymentPeriod.CalculateSummary(Array.Empty<WorkLog>()));
    }

    [Fact]
    public void CalculateSummary_SingleLog_ReturnsTotals()
    {
        var logs = new List<WorkLog>
        {
            new() { HoursWorked = 8m, HourlyRateAtTime = 15m, TotalAmount = 120m }
        };

        var (totalHours, totalAmount) = PaymentPeriod.CalculateSummary(logs);

        Assert.Equal(8m, totalHours);
        Assert.Equal(120m, totalAmount);
    }

    [Fact]
    public void CalculateSummary_MultipleLogs_SumsAll()
    {
        var logs = new List<WorkLog>
        {
            new() { HoursWorked = 8m,   HourlyRateAtTime = 15m, TotalAmount = 120.00m },
            new() { HoursWorked = 7.5m, HourlyRateAtTime = 15m, TotalAmount = 112.50m },
            new() { HoursWorked = 6m,   HourlyRateAtTime = 15m, TotalAmount = 90.00m  }
        };

        var (totalHours, totalAmount) = PaymentPeriod.CalculateSummary(logs);

        Assert.Equal(21.5m, totalHours);
        Assert.Equal(322.50m, totalAmount);
    }
}
```

- [ ] **Step 3: Run tests to confirm they fail (types not defined yet)**

```bash
dotnet test --filter "FullyQualifiedName~WorkLogTests|FullyQualifiedName~PaymentPeriodTests"
```

Expected: Build error — `WorkLog` and `PaymentPeriod` types not found.

- [ ] **Step 4: Create Employee.cs**

```csharp
using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class Employee : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public string FullName { get; set; } = string.Empty;
        public string CPF { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTime HireDate { get; set; }
        public DateTime? TerminationDate { get; set; }

        public ContractType ContractType { get; set; }
        public decimal HourlyRate { get; set; }

        public HrPaymentMethod PreferredPaymentMethod { get; set; }
        public string? PixKey { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAgency { get; set; }

        public bool HasSignedContract { get; set; }
        public DateTime? ContractSignedDate { get; set; }

        public Guid? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        public ICollection<WorkLog> WorkLogs { get; set; } = new List<WorkLog>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
```

- [ ] **Step 5: Create WorkLog.cs**

```csharp
using SaaS_BasePlatform.Domain.Common;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class WorkLog : EntityBase
    {
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public DateTime WorkDate { get; set; }
        public decimal HoursWorked { get; set; }
        public decimal HourlyRateAtTime { get; set; }
        public decimal TotalAmount { get; set; }

        public string? Notes { get; set; }

        public Guid? PaymentPeriodId { get; set; }
        public PaymentPeriod? PaymentPeriod { get; set; }

        public static decimal CalculateTotalAmount(decimal hoursWorked, decimal hourlyRate)
        {
            if (hoursWorked <= 0) throw new ArgumentException("Hours worked must be greater than zero.");
            if (hourlyRate <= 0) throw new ArgumentException("Hourly rate must be greater than zero.");
            return Math.Round(hoursWorked * hourlyRate, 2);
        }
    }
}
```

- [ ] **Step 6: Create PaymentPeriod.cs**

```csharp
using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class PaymentPeriod : EntityBase
    {
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public decimal TotalHours { get; set; }
        public decimal TotalAmount { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        public ICollection<WorkLog> WorkLogs { get; set; } = new List<WorkLog>();
        public Payment? Payment { get; set; }

        public static (decimal TotalHours, decimal TotalAmount) CalculateSummary(
            IReadOnlyList<WorkLog> logs)
        {
            if (logs.Count == 0)
                throw new InvalidOperationException(
                    "Cannot generate a payment period without work logs.");
            return (logs.Sum(l => l.HoursWorked), logs.Sum(l => l.TotalAmount));
        }
    }
}
```

- [ ] **Step 7: Create Payment.cs**

```csharp
using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Domain.Entities
{
    public class Payment : EntityBase
    {
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public Guid PaymentPeriodId { get; set; }
        public PaymentPeriod PaymentPeriod { get; set; } = null!;

        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public HrPaymentMethod PaymentMethod { get; set; }

        public string? PaymentProof { get; set; }
        public string? Notes { get; set; }

        public Guid PaidByUserId { get; set; }
        public ApplicationUser PaidByUser { get; set; } = null!;
    }
}
```

- [ ] **Step 8: Run tests — expect pass**

```bash
dotnet test --filter "FullyQualifiedName~WorkLogTests|FullyQualifiedName~PaymentPeriodTests"
```

Expected: 7 tests passing.

- [ ] **Step 9: Commit**

```bash
git add SaaS_BasePlatform.Domain/Entities/Employee.cs \
        SaaS_BasePlatform.Domain/Entities/WorkLog.cs \
        SaaS_BasePlatform.Domain/Entities/PaymentPeriod.cs \
        SaaS_BasePlatform.Domain/Entities/Payment.cs \
        SaaS_BasePlatform.Tests/Domain/WorkLogTests.cs \
        SaaS_BasePlatform.Tests/Domain/PaymentPeriodTests.cs
git commit -m "feat: add HR domain entities with calculation methods and tests"
```

---

## Task 4: Application DTOs

**Files:**
- Create: `SaaS_BasePlatform.Application/DTOs/HR/EmployeeDtos.cs`
- Create: `SaaS_BasePlatform.Application/DTOs/HR/WorkLogDtos.cs`
- Create: `SaaS_BasePlatform.Application/DTOs/HR/PaymentPeriodDtos.cs`
- Create: `SaaS_BasePlatform.Application/DTOs/HR/PaymentDtos.cs`

- [ ] **Step 1: Create EmployeeDtos.cs**

```csharp
namespace SaaS_BasePlatform.Application.DTOs.HR
{
    public record EmployeeDto(
        Guid Id,
        Guid TenantId,
        string FullName,
        string CPF,
        string? Phone,
        string? Email,
        DateTime HireDate,
        DateTime? TerminationDate,
        bool IsActive,
        int ContractType,
        string ContractTypeName,
        decimal HourlyRate,
        int PreferredPaymentMethod,
        string PreferredPaymentMethodName,
        string? PixKey,
        string? BankName,
        string? BankAccountNumber,
        string? BankAgency,
        bool HasSignedContract,
        DateTime? ContractSignedDate,
        Guid? ApplicationUserId,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    public record CreateEmployeeRequestDto(
        string FullName,
        string CPF,
        string? Phone,
        string? Email,
        DateTime HireDate,
        int ContractType,
        decimal HourlyRate,
        int PreferredPaymentMethod,
        string? PixKey,
        string? BankName,
        string? BankAccountNumber,
        string? BankAgency,
        bool HasSignedContract,
        Guid? ApplicationUserId
    );

    public record UpdateEmployeeRequestDto(
        string FullName,
        string? Phone,
        string? Email,
        bool IsActive,
        int ContractType,
        decimal HourlyRate,
        int PreferredPaymentMethod,
        string? PixKey,
        string? BankName,
        string? BankAccountNumber,
        string? BankAgency,
        bool HasSignedContract,
        DateTime? TerminationDate
    );
}
```

- [ ] **Step 2: Create WorkLogDtos.cs**

```csharp
namespace SaaS_BasePlatform.Application.DTOs.HR
{
    public record WorkLogDto(
        Guid Id,
        Guid EmployeeId,
        string EmployeeName,
        DateTime WorkDate,
        decimal HoursWorked,
        decimal HourlyRateAtTime,
        decimal TotalAmount,
        string? Notes,
        Guid? PaymentPeriodId,
        DateTime CreatedAt
    );

    public record CreateWorkLogRequestDto(
        Guid EmployeeId,
        DateTime WorkDate,
        decimal HoursWorked,
        string? Notes
    );

    public record UpdateWorkLogRequestDto(
        DateTime WorkDate,
        decimal HoursWorked,
        string? Notes
    );

    public record WorkLogQueryDto(
        DateTime? From = null,
        DateTime? To = null,
        bool OnlyUnassigned = false
    );
}
```

- [ ] **Step 3: Create PaymentPeriodDtos.cs**

```csharp
namespace SaaS_BasePlatform.Application.DTOs.HR
{
    public record PaymentPeriodDto(
        Guid Id,
        Guid EmployeeId,
        string EmployeeName,
        DateTime StartDate,
        DateTime EndDate,
        decimal TotalHours,
        decimal TotalAmount,
        int Status,
        string StatusName,
        IReadOnlyList<WorkLogDto> WorkLogs,
        DateTime CreatedAt
    );

    public record PaymentPeriodSummaryDto(
        Guid Id,
        Guid EmployeeId,
        string EmployeeName,
        DateTime StartDate,
        DateTime EndDate,
        decimal TotalHours,
        decimal TotalAmount,
        int Status,
        string StatusName,
        DateTime CreatedAt
    );

    public record GeneratePaymentPeriodRequestDto(
        Guid EmployeeId,
        DateTime StartDate,
        DateTime EndDate
    );

    public record UpdatePaymentPeriodStatusDto(int Status);
}
```

- [ ] **Step 4: Create PaymentDtos.cs**

```csharp
namespace SaaS_BasePlatform.Application.DTOs.HR
{
    public record PaymentDto(
        Guid Id,
        Guid EmployeeId,
        string EmployeeName,
        Guid PaymentPeriodId,
        DateTime PaymentDate,
        decimal Amount,
        int PaymentMethod,
        string PaymentMethodName,
        string? PaymentProof,
        string? Notes,
        Guid PaidByUserId,
        string PaidByUserName,
        DateTime CreatedAt
    );

    public record CreatePaymentRequestDto(
        Guid PaymentPeriodId,
        DateTime PaymentDate,
        int PaymentMethod,
        string? PaymentProof,
        string? Notes
    );
}
```

- [ ] **Step 5: Build Application**

```bash
dotnet build SaaS_BasePlatform.Application
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 6: Commit**

```bash
git add SaaS_BasePlatform.Application/DTOs/HR/
git commit -m "feat: add HR DTOs (employee, worklog, payment period, payment)"
```

---

## Task 5: Employee Service

**Files:**
- Create: `SaaS_BasePlatform.Application/Services/IEmployeeService.cs`
- Create: `SaaS_BasePlatform.Application/Services/EmployeeService.cs`

- [ ] **Step 1: Create IEmployeeService.cs**

```csharp
using SaaS_BasePlatform.Application.DTOs.HR;

namespace SaaS_BasePlatform.Application.Services
{
    public interface IEmployeeService
    {
        Task<IReadOnlyList<EmployeeDto>> ListAsync(Guid tenantId, bool includeInactive = false, CancellationToken ct = default);
        Task<EmployeeDto?> GetAsync(Guid tenantId, Guid employeeId, CancellationToken ct = default);
        Task<EmployeeDto> CreateAsync(Guid tenantId, CreateEmployeeRequestDto request, CancellationToken ct = default);
        Task<EmployeeDto> UpdateAsync(Guid tenantId, Guid employeeId, UpdateEmployeeRequestDto request, CancellationToken ct = default);
        Task DeactivateAsync(Guid tenantId, Guid employeeId, CancellationToken ct = default);
    }
}
```

- [ ] **Step 2: Create EmployeeService.cs**

```csharp
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
```

- [ ] **Step 3: Commit**

```bash
git add SaaS_BasePlatform.Application/Services/IEmployeeService.cs \
        SaaS_BasePlatform.Application/Services/EmployeeService.cs
git commit -m "feat: add IEmployeeService and EmployeeService"
```

---

## Task 6: WorkLog Service

**Files:**
- Create: `SaaS_BasePlatform.Application/Services/IWorkLogService.cs`
- Create: `SaaS_BasePlatform.Application/Services/WorkLogService.cs`

- [ ] **Step 1: Create IWorkLogService.cs**

```csharp
using SaaS_BasePlatform.Application.DTOs.HR;

namespace SaaS_BasePlatform.Application.Services
{
    public interface IWorkLogService
    {
        Task<IReadOnlyList<WorkLogDto>> ListByEmployeeAsync(Guid tenantId, Guid employeeId, WorkLogQueryDto query, CancellationToken ct = default);
        Task<WorkLogDto?> GetByIdAsync(Guid tenantId, Guid workLogId, CancellationToken ct = default);
        Task<WorkLogDto> CreateAsync(Guid tenantId, CreateWorkLogRequestDto request, CancellationToken ct = default);
        Task<WorkLogDto> UpdateAsync(Guid tenantId, Guid workLogId, UpdateWorkLogRequestDto request, CancellationToken ct = default);
        Task DeleteAsync(Guid tenantId, Guid workLogId, CancellationToken ct = default);
    }
}
```

- [ ] **Step 2: Create WorkLogService.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Application.DTOs.HR;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Infrastructure.Data;

namespace SaaS_BasePlatform.Application.Services
{
    public class WorkLogService : IWorkLogService
    {
        private readonly ApplicationDbContext _db;

        public WorkLogService(ApplicationDbContext db) => _db = db;

        public async Task<IReadOnlyList<WorkLogDto>> ListByEmployeeAsync(
            Guid tenantId, Guid employeeId, WorkLogQueryDto query, CancellationToken ct = default)
        {
            await RequireEmployeeAsync(tenantId, employeeId, ct);

            var q = _db.WorkLogs.IgnoreQueryFilters()
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
            var w = await _db.WorkLogs.IgnoreQueryFilters()
                .Include(w => w.Employee)
                .FirstOrDefaultAsync(w => w.Id == workLogId && w.Employee.TenantId == tenantId, ct);
            return w == null ? null : ToDto(w);
        }

        public async Task<WorkLogDto> CreateAsync(
            Guid tenantId, CreateWorkLogRequestDto request, CancellationToken ct = default)
        {
            var employee = await _db.Employees.IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == request.EmployeeId, ct)
                ?? throw new KeyNotFoundException("Employee not found.");

            if (!employee.IsActive)
                throw new InvalidOperationException("Cannot log hours for an inactive employee.");

            if (request.HoursWorked <= 0 || request.HoursWorked > 24)
                throw new ArgumentException("HoursWorked must be between 0 and 24.");

            var workDate = DateTime.SpecifyKind(request.WorkDate.Date, DateTimeKind.Utc);

            var duplicate = await _db.WorkLogs.IgnoreQueryFilters()
                .AnyAsync(w => w.EmployeeId == request.EmployeeId && w.WorkDate == workDate, ct);
            if (duplicate)
                throw new InvalidOperationException(
                    $"A work log already exists for {employee.FullName} on {workDate:yyyy-MM-dd}.");

            var workLog = new WorkLog
            {
                EmployeeId      = request.EmployeeId,
                WorkDate        = workDate,
                HoursWorked     = request.HoursWorked,
                HourlyRateAtTime = employee.HourlyRate,
                TotalAmount     = WorkLog.CalculateTotalAmount(request.HoursWorked, employee.HourlyRate),
                Notes           = request.Notes?.Trim()
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
            var workLog = await _db.WorkLogs.IgnoreQueryFilters()
                .Include(w => w.Employee)
                .FirstOrDefaultAsync(w => w.Id == workLogId && w.Employee.TenantId == tenantId, ct)
                ?? throw new KeyNotFoundException("WorkLog not found.");

            if (workLog.PaymentPeriodId.HasValue)
                throw new InvalidOperationException(
                    "Cannot edit a work log that is linked to a payment period.");

            if (request.HoursWorked <= 0 || request.HoursWorked > 24)
                throw new ArgumentException("HoursWorked must be between 0 and 24.");

            var workDate = DateTime.SpecifyKind(request.WorkDate.Date, DateTimeKind.Utc);

            if (workDate != workLog.WorkDate)
            {
                var duplicate = await _db.WorkLogs.IgnoreQueryFilters()
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
            var workLog = await _db.WorkLogs.IgnoreQueryFilters()
                .Include(w => w.Employee)
                .FirstOrDefaultAsync(w => w.Id == workLogId && w.Employee.TenantId == tenantId, ct)
                ?? throw new KeyNotFoundException("WorkLog not found.");

            if (workLog.PaymentPeriodId.HasValue)
                throw new InvalidOperationException(
                    "Cannot delete a work log that is linked to a payment period.");

            _db.WorkLogs.Remove(workLog);
            await _db.SaveChangesAsync(ct);
        }

        private async Task RequireEmployeeAsync(Guid tenantId, Guid employeeId, CancellationToken ct)
        {
            var exists = await _db.Employees.IgnoreQueryFilters()
                .AnyAsync(e => e.TenantId == tenantId && e.Id == employeeId, ct);
            if (!exists) throw new KeyNotFoundException("Employee not found.");
        }

        private static WorkLogDto ToDto(WorkLog w) => new(
            w.Id, w.EmployeeId, w.Employee?.FullName ?? string.Empty,
            w.WorkDate, w.HoursWorked, w.HourlyRateAtTime, w.TotalAmount,
            w.Notes, w.PaymentPeriodId, w.CreatedAt);
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add SaaS_BasePlatform.Application/Services/IWorkLogService.cs \
        SaaS_BasePlatform.Application/Services/WorkLogService.cs
git commit -m "feat: add IWorkLogService and WorkLogService"
```

---

## Task 7: Payment Period Service

**Files:**
- Create: `SaaS_BasePlatform.Application/Services/IPaymentPeriodService.cs`
- Create: `SaaS_BasePlatform.Application/Services/PaymentPeriodService.cs`

- [ ] **Step 1: Create IPaymentPeriodService.cs**

```csharp
using SaaS_BasePlatform.Application.DTOs.HR;
using SaaS_BasePlatform.Domain.Enums;

namespace SaaS_BasePlatform.Application.Services
{
    public interface IPaymentPeriodService
    {
        Task<PaymentPeriodDto> GenerateAsync(Guid tenantId, GeneratePaymentPeriodRequestDto request, CancellationToken ct = default);
        Task<PaymentPeriodDto?> GetByIdAsync(Guid tenantId, Guid periodId, CancellationToken ct = default);
        Task<IReadOnlyList<PaymentPeriodSummaryDto>> ListByEmployeeAsync(Guid tenantId, Guid employeeId, CancellationToken ct = default);
        Task UpdateStatusAsync(Guid tenantId, Guid periodId, PaymentStatus status, CancellationToken ct = default);
        Task DeleteAsync(Guid tenantId, Guid periodId, CancellationToken ct = default);
    }
}
```

- [ ] **Step 2: Create PaymentPeriodService.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Application.DTOs.HR;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Data;

namespace SaaS_BasePlatform.Application.Services
{
    public class PaymentPeriodService : IPaymentPeriodService
    {
        private readonly ApplicationDbContext _db;

        public PaymentPeriodService(ApplicationDbContext db) => _db = db;

        public async Task<PaymentPeriodDto> GenerateAsync(
            Guid tenantId, GeneratePaymentPeriodRequestDto request, CancellationToken ct = default)
        {
            var employee = await _db.Employees.IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == request.EmployeeId, ct)
                ?? throw new KeyNotFoundException("Employee not found.");

            var startDate = DateTime.SpecifyKind(request.StartDate.Date, DateTimeKind.Utc);
            var endDate   = DateTime.SpecifyKind(request.EndDate.Date,   DateTimeKind.Utc);

            if (startDate >= endDate)
                throw new ArgumentException("StartDate must be before EndDate.");

            var duplicate = await _db.PaymentPeriods.IgnoreQueryFilters()
                .AnyAsync(p => p.EmployeeId == request.EmployeeId
                            && p.StartDate == startDate
                            && p.EndDate   == endDate, ct);
            if (duplicate)
                throw new InvalidOperationException(
                    "A payment period for this employee and date range already exists.");

            var workLogs = await _db.WorkLogs.IgnoreQueryFilters()
                .Where(w => w.EmployeeId == request.EmployeeId
                         && w.WorkDate >= startDate
                         && w.WorkDate <= endDate
                         && w.PaymentPeriodId == null)
                .ToListAsync(ct);

            if (!workLogs.Any())
                throw new InvalidOperationException(
                    "No unassigned work logs found for the specified date range.");

            var (totalHours, totalAmount) = PaymentPeriod.CalculateSummary(workLogs);

            var period = new PaymentPeriod
            {
                EmployeeId  = request.EmployeeId,
                StartDate   = startDate,
                EndDate     = endDate,
                TotalHours  = totalHours,
                TotalAmount = totalAmount,
                Status      = PaymentStatus.Pending
            };

            _db.PaymentPeriods.Add(period);

            foreach (var wl in workLogs)
                wl.PaymentPeriodId = period.Id;

            await _db.SaveChangesAsync(ct);

            return await GetByIdAsync(tenantId, period.Id, ct)
                ?? throw new InvalidOperationException("Failed to read created payment period.");
        }

        public async Task<PaymentPeriodDto?> GetByIdAsync(
            Guid tenantId, Guid periodId, CancellationToken ct = default)
        {
            var period = await _db.PaymentPeriods.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Include(p => p.WorkLogs)
                .FirstOrDefaultAsync(p => p.Id == periodId && p.Employee.TenantId == tenantId, ct);
            return period == null ? null : ToDto(period);
        }

        public async Task<IReadOnlyList<PaymentPeriodSummaryDto>> ListByEmployeeAsync(
            Guid tenantId, Guid employeeId, CancellationToken ct = default)
        {
            var exists = await _db.Employees.IgnoreQueryFilters()
                .AnyAsync(e => e.TenantId == tenantId && e.Id == employeeId, ct);
            if (!exists) throw new KeyNotFoundException("Employee not found.");

            return await _db.PaymentPeriods.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Where(p => p.EmployeeId == employeeId)
                .OrderByDescending(p => p.StartDate)
                .Select(p => ToSummaryDto(p))
                .ToListAsync(ct);
        }

        public async Task UpdateStatusAsync(
            Guid tenantId, Guid periodId, PaymentStatus status, CancellationToken ct = default)
        {
            var period = await _db.PaymentPeriods.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.Id == periodId && p.Employee.TenantId == tenantId, ct)
                ?? throw new KeyNotFoundException("Payment period not found.");
            period.Status = status;
            await _db.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(
            Guid tenantId, Guid periodId, CancellationToken ct = default)
        {
            var period = await _db.PaymentPeriods.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Include(p => p.Payment)
                .FirstOrDefaultAsync(p => p.Id == periodId && p.Employee.TenantId == tenantId, ct)
                ?? throw new KeyNotFoundException("Payment period not found.");

            if (period.Payment != null)
                throw new InvalidOperationException(
                    "Cannot delete a payment period that has an associated payment.");

            await _db.WorkLogs.IgnoreQueryFilters()
                .Where(w => w.PaymentPeriodId == periodId)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.PaymentPeriodId, (Guid?)null), ct);

            _db.PaymentPeriods.Remove(period);
            await _db.SaveChangesAsync(ct);
        }

        private static PaymentPeriodDto ToDto(PaymentPeriod p) => new(
            p.Id, p.EmployeeId, p.Employee.FullName,
            p.StartDate, p.EndDate, p.TotalHours, p.TotalAmount,
            (int)p.Status, p.Status.ToString(),
            p.WorkLogs.Select(w => new WorkLogDto(
                w.Id, w.EmployeeId, p.Employee.FullName,
                w.WorkDate, w.HoursWorked, w.HourlyRateAtTime, w.TotalAmount,
                w.Notes, w.PaymentPeriodId, w.CreatedAt)).ToList(),
            p.CreatedAt);

        private static PaymentPeriodSummaryDto ToSummaryDto(PaymentPeriod p) => new(
            p.Id, p.EmployeeId, p.Employee.FullName,
            p.StartDate, p.EndDate, p.TotalHours, p.TotalAmount,
            (int)p.Status, p.Status.ToString(), p.CreatedAt);
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add SaaS_BasePlatform.Application/Services/IPaymentPeriodService.cs \
        SaaS_BasePlatform.Application/Services/PaymentPeriodService.cs
git commit -m "feat: add IPaymentPeriodService and PaymentPeriodService"
```

---

## Task 8: Payment Service

**Files:**
- Create: `SaaS_BasePlatform.Application/Services/IPaymentService.cs`
- Create: `SaaS_BasePlatform.Application/Services/PaymentService.cs`

- [ ] **Step 1: Create IPaymentService.cs**

```csharp
using SaaS_BasePlatform.Application.DTOs.HR;

namespace SaaS_BasePlatform.Application.Services
{
    public interface IPaymentService
    {
        Task<PaymentDto> CreateAsync(Guid tenantId, Guid paidByUserId, CreatePaymentRequestDto request, CancellationToken ct = default);
        Task<PaymentDto?> GetByIdAsync(Guid tenantId, Guid paymentId, CancellationToken ct = default);
        Task<IReadOnlyList<PaymentDto>> ListRecentAsync(Guid tenantId, int count = 20, CancellationToken ct = default);
        Task<IReadOnlyList<PaymentDto>> ListByEmployeeAsync(Guid tenantId, Guid employeeId, CancellationToken ct = default);
        Task DeleteAsync(Guid tenantId, Guid paymentId, CancellationToken ct = default);
    }
}
```

- [ ] **Step 2: Create PaymentService.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Application.DTOs.HR;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Data;

namespace SaaS_BasePlatform.Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _db;

        public PaymentService(ApplicationDbContext db) => _db = db;

        public async Task<PaymentDto> CreateAsync(
            Guid tenantId, Guid paidByUserId, CreatePaymentRequestDto request, CancellationToken ct = default)
        {
            if (!Enum.IsDefined(typeof(HrPaymentMethod), request.PaymentMethod))
                throw new ArgumentException($"Invalid PaymentMethod: {request.PaymentMethod}.");

            var period = await _db.PaymentPeriods.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Include(p => p.Payment)
                .FirstOrDefaultAsync(p => p.Id == request.PaymentPeriodId
                                       && p.Employee.TenantId == tenantId, ct)
                ?? throw new KeyNotFoundException("Payment period not found.");

            if (period.Payment != null)
                throw new InvalidOperationException(
                    "This payment period already has an associated payment.");

            if (period.Status != PaymentStatus.Pending)
                throw new InvalidOperationException(
                    "Only Pending payment periods can be paid.");

            var payment = new Payment
            {
                EmployeeId     = period.EmployeeId,
                PaymentPeriodId = period.Id,
                PaymentDate    = DateTime.SpecifyKind(request.PaymentDate, DateTimeKind.Utc),
                Amount         = period.TotalAmount,
                PaymentMethod  = (HrPaymentMethod)request.PaymentMethod,
                PaymentProof   = request.PaymentProof?.Trim(),
                Notes          = request.Notes?.Trim(),
                PaidByUserId   = paidByUserId
            };

            period.Status = PaymentStatus.Paid;
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync(ct);

            return await GetByIdAsync(tenantId, payment.Id, ct)
                ?? throw new InvalidOperationException("Failed to read created payment.");
        }

        public async Task<PaymentDto?> GetByIdAsync(
            Guid tenantId, Guid paymentId, CancellationToken ct = default)
        {
            var p = await _db.Payments.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Include(p => p.PaidByUser)
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.Employee.TenantId == tenantId, ct);
            return p == null ? null : ToDto(p);
        }

        public async Task<IReadOnlyList<PaymentDto>> ListRecentAsync(
            Guid tenantId, int count = 20, CancellationToken ct = default)
        {
            return await _db.Payments.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Include(p => p.PaidByUser)
                .Where(p => p.Employee.TenantId == tenantId)
                .OrderByDescending(p => p.PaymentDate)
                .Take(Math.Min(count, 100))
                .Select(p => ToDto(p))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<PaymentDto>> ListByEmployeeAsync(
            Guid tenantId, Guid employeeId, CancellationToken ct = default)
        {
            var exists = await _db.Employees.IgnoreQueryFilters()
                .AnyAsync(e => e.TenantId == tenantId && e.Id == employeeId, ct);
            if (!exists) throw new KeyNotFoundException("Employee not found.");

            return await _db.Payments.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Include(p => p.PaidByUser)
                .Where(p => p.EmployeeId == employeeId)
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => ToDto(p))
                .ToListAsync(ct);
        }

        public async Task DeleteAsync(
            Guid tenantId, Guid paymentId, CancellationToken ct = default)
        {
            var payment = await _db.Payments.IgnoreQueryFilters()
                .Include(p => p.Employee)
                .Include(p => p.PaymentPeriod)
                .FirstOrDefaultAsync(p => p.Id == paymentId && p.Employee.TenantId == tenantId, ct)
                ?? throw new KeyNotFoundException("Payment not found.");

            payment.PaymentPeriod.Status = PaymentStatus.Pending;
            _db.Payments.Remove(payment);
            await _db.SaveChangesAsync(ct);
        }

        private static PaymentDto ToDto(Payment p) => new(
            p.Id, p.EmployeeId, p.Employee.FullName,
            p.PaymentPeriodId, p.PaymentDate, p.Amount,
            (int)p.PaymentMethod, p.PaymentMethod.ToString(),
            p.PaymentProof, p.Notes,
            p.PaidByUserId, p.PaidByUser?.FullName ?? string.Empty,
            p.CreatedAt);
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add SaaS_BasePlatform.Application/Services/IPaymentService.cs \
        SaaS_BasePlatform.Application/Services/PaymentService.cs
git commit -m "feat: add IPaymentService and PaymentService"
```

---

## Task 9: EF Core Configurations + DbContext

**Files:**
- Create: `SaaS_BasePlatform.Infrastructure/Data/Configurations/EmployeeConfiguration.cs`
- Create: `SaaS_BasePlatform.Infrastructure/Data/Configurations/WorkLogConfiguration.cs`
- Create: `SaaS_BasePlatform.Infrastructure/Data/Configurations/PaymentPeriodConfiguration.cs`
- Create: `SaaS_BasePlatform.Infrastructure/Data/Configurations/PaymentConfiguration.cs`
- Modify: `SaaS_BasePlatform.Infrastructure/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Create EmployeeConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.ToTable("Employees");
            builder.HasKey(e => e.Id);

            builder.Property(e => e.FullName).IsRequired().HasMaxLength(200);
            builder.Property(e => e.CPF).IsRequired().HasMaxLength(14);
            builder.Property(e => e.Phone).HasMaxLength(20);
            builder.Property(e => e.Email).HasMaxLength(200);
            builder.Property(e => e.HourlyRate).HasColumnType("decimal(18,2)");
            builder.Property(e => e.PixKey).HasMaxLength(100);
            builder.Property(e => e.BankName).HasMaxLength(100);
            builder.Property(e => e.BankAccountNumber).HasMaxLength(20);
            builder.Property(e => e.BankAgency).HasMaxLength(10);
            builder.Property(e => e.ContractType).HasConversion<int>();
            builder.Property(e => e.PreferredPaymentMethod).HasConversion<int>();

            builder.HasIndex(e => new { e.TenantId, e.CPF }).IsUnique();
            builder.HasIndex(e => new { e.TenantId, e.IsActive });

            builder.HasOne(e => e.ApplicationUser)
                .WithMany()
                .HasForeignKey(e => e.ApplicationUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
```

- [ ] **Step 2: Create WorkLogConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class WorkLogConfiguration : IEntityTypeConfiguration<WorkLog>
    {
        public void Configure(EntityTypeBuilder<WorkLog> builder)
        {
            builder.ToTable("WorkLogs");
            builder.HasKey(w => w.Id);

            builder.Property(w => w.HoursWorked).HasColumnType("decimal(18,2)");
            builder.Property(w => w.HourlyRateAtTime).HasColumnType("decimal(18,2)");
            builder.Property(w => w.TotalAmount).HasColumnType("decimal(18,2)");
            builder.Property(w => w.Notes).HasMaxLength(500);

            builder.HasIndex(w => new { w.EmployeeId, w.WorkDate }).IsUnique();
            builder.HasIndex(w => w.PaymentPeriodId);

            builder.HasOne(w => w.Employee)
                .WithMany(e => e.WorkLogs)
                .HasForeignKey(w => w.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

- [ ] **Step 3: Create PaymentPeriodConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class PaymentPeriodConfiguration : IEntityTypeConfiguration<PaymentPeriod>
    {
        public void Configure(EntityTypeBuilder<PaymentPeriod> builder)
        {
            builder.ToTable("PaymentPeriods");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.TotalHours).HasColumnType("decimal(18,2)");
            builder.Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");
            builder.Property(p => p.Status).HasConversion<int>();

            builder.HasIndex(p => new { p.EmployeeId, p.StartDate, p.EndDate });
            builder.HasIndex(p => p.Status);

            builder.HasMany(p => p.WorkLogs)
                .WithOne(w => w.PaymentPeriod)
                .HasForeignKey(w => w.PaymentPeriodId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(p => p.Payment)
                .WithOne(pay => pay.PaymentPeriod)
                .HasForeignKey<Payment>(pay => pay.PaymentPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
```

- [ ] **Step 4: Create PaymentConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS_BasePlatform.Domain.Entities;

namespace SaaS_BasePlatform.Infrastructure.Data.Configurations
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            builder.Property(p => p.PaymentMethod).HasConversion<int>();
            builder.Property(p => p.PaymentProof).HasMaxLength(500);
            builder.Property(p => p.Notes).HasMaxLength(500);

            builder.HasIndex(p => p.PaymentDate);
            builder.HasIndex(p => p.EmployeeId);

            builder.HasOne(p => p.Employee)
                .WithMany(e => e.Payments)
                .HasForeignKey(p => p.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.PaidByUser)
                .WithMany()
                .HasForeignKey(p => p.PaidByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
```

- [ ] **Step 5: Add DbSets to ApplicationDbContext**

Open `SaaS_BasePlatform.Infrastructure/Data/ApplicationDbContext.cs`. After the `// General Ledger` block (line ~44), add:

```csharp
        // HR Module
        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<WorkLog> WorkLogs => Set<WorkLog>();
        public DbSet<PaymentPeriod> PaymentPeriods => Set<PaymentPeriod>();
        public DbSet<Payment> Payments => Set<Payment>();
```

- [ ] **Step 6: Build full solution**

```bash
dotnet build
```

Expected: Build succeeded, 0 error(s). All service files referencing `_db.Employees` etc. now compile.

- [ ] **Step 7: Run all tests**

```bash
dotnet test
```

Expected: All tests pass (including the 7 new HR domain tests).

- [ ] **Step 8: Commit**

```bash
git add SaaS_BasePlatform.Infrastructure/Data/Configurations/EmployeeConfiguration.cs \
        SaaS_BasePlatform.Infrastructure/Data/Configurations/WorkLogConfiguration.cs \
        SaaS_BasePlatform.Infrastructure/Data/Configurations/PaymentPeriodConfiguration.cs \
        SaaS_BasePlatform.Infrastructure/Data/Configurations/PaymentConfiguration.cs \
        SaaS_BasePlatform.Infrastructure/Data/ApplicationDbContext.cs
git commit -m "feat: add EF configurations and DbSets for HR entities"
```

---

## Task 10: DI Registration

**Files:**
- Modify: `SaaS_BasePlatform.Api/Configuration/DependencyInjectionConfiguration.cs`

- [ ] **Step 1: Register HR services**

Open `SaaS_BasePlatform.Api/Configuration/DependencyInjectionConfiguration.cs`. After the `// General Ledger` block, add:

```csharp
        // HR Module
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IWorkLogService, WorkLogService>();
        services.AddScoped<IPaymentPeriodService, PaymentPeriodService>();
        services.AddScoped<IPaymentService, PaymentService>();
```

- [ ] **Step 2: Build full solution**

```bash
dotnet build
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 3: Commit**

```bash
git add SaaS_BasePlatform.Api/Configuration/DependencyInjectionConfiguration.cs
git commit -m "feat: register HR services in DI"
```

---

## Task 11: Controllers

**Files:**
- Create: `SaaS_BasePlatform.Api/Controllers/EmployeesController.cs`
- Create: `SaaS_BasePlatform.Api/Controllers/WorkLogsController.cs`
- Create: `SaaS_BasePlatform.Api/Controllers/PaymentPeriodsController.cs`
- Create: `SaaS_BasePlatform.Api/Controllers/PaymentsController.cs`

- [ ] **Step 1: Create EmployeesController.cs**

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS_BasePlatform.Application.DTOs.HR;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Enums;
using System.Security.Claims;

namespace SaaS_BasePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/employees")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class EmployeesController : ControllerBase
    {
        private readonly IEmployeeService _service;
        private readonly ITenantService _tenantService;

        public EmployeesController(IEmployeeService service, ITenantService tenantService)
        {
            _service       = service;
            _tenantService = tenantService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private async Task<TenantRole?> GetRoleAsync(Guid tenantId, CancellationToken ct) =>
            await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);

        private static bool CanAccess(TenantRole? role) => role.HasValue;
        private static bool CanManage(TenantRole? role) => role is TenantRole.Owner or TenantRole.Admin;

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<EmployeeDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> List(
            Guid tenantId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.ListAsync(tenantId, includeInactive, ct));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EmployeeDto>> GetById(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            var employee = await _service.GetAsync(tenantId, id, ct);
            return employee == null ? NotFound() : Ok(employee);
        }

        [HttpPost]
        [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<EmployeeDto>> Create(
            Guid tenantId, [FromBody] CreateEmployeeRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            var employee = await _service.CreateAsync(tenantId, request, ct);
            return CreatedAtAction(nameof(GetById), new { tenantId, id = employee.Id }, employee);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EmployeeDto>> Update(
            Guid tenantId, Guid id, [FromBody] UpdateEmployeeRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.UpdateAsync(tenantId, id, request, ct));
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Deactivate(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            await _service.DeactivateAsync(tenantId, id, ct);
            return NoContent();
        }
    }
}
```

- [ ] **Step 2: Create WorkLogsController.cs**

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS_BasePlatform.Application.DTOs.HR;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Enums;
using System.Security.Claims;

namespace SaaS_BasePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/employees/{employeeId:guid}/worklogs")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class WorkLogsController : ControllerBase
    {
        private readonly IWorkLogService _service;
        private readonly ITenantService _tenantService;

        public WorkLogsController(IWorkLogService service, ITenantService tenantService)
        {
            _service       = service;
            _tenantService = tenantService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private async Task<TenantRole?> GetRoleAsync(Guid tenantId, CancellationToken ct) =>
            await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);

        private static bool CanAccess(TenantRole? role) => role.HasValue;
        private static bool CanManage(TenantRole? role) => role is TenantRole.Owner or TenantRole.Admin;

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<WorkLogDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<WorkLogDto>>> List(
            Guid tenantId, Guid employeeId, [FromQuery] WorkLogQueryDto query, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.ListByEmployeeAsync(tenantId, employeeId, query, ct));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(WorkLogDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<WorkLogDto>> GetById(
            Guid tenantId, Guid employeeId, Guid id, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            var log = await _service.GetByIdAsync(tenantId, id, ct);
            return log == null ? NotFound() : Ok(log);
        }

        [HttpPost]
        [ProducesResponseType(typeof(WorkLogDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<WorkLogDto>> Create(
            Guid tenantId, Guid employeeId,
            [FromBody] CreateWorkLogRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            var actualRequest = request with { EmployeeId = employeeId };
            var log = await _service.CreateAsync(tenantId, actualRequest, ct);
            return CreatedAtAction(nameof(GetById), new { tenantId, employeeId, id = log.Id }, log);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(WorkLogDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<WorkLogDto>> Update(
            Guid tenantId, Guid employeeId, Guid id,
            [FromBody] UpdateWorkLogRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.UpdateAsync(tenantId, id, request, ct));
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            Guid tenantId, Guid employeeId, Guid id, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            await _service.DeleteAsync(tenantId, id, ct);
            return NoContent();
        }
    }
}
```

- [ ] **Step 3: Create PaymentPeriodsController.cs**

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS_BasePlatform.Application.DTOs.HR;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Enums;
using System.Security.Claims;

namespace SaaS_BasePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/employees/{employeeId:guid}/payment-periods")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class PaymentPeriodsController : ControllerBase
    {
        private readonly IPaymentPeriodService _service;
        private readonly ITenantService _tenantService;

        public PaymentPeriodsController(IPaymentPeriodService service, ITenantService tenantService)
        {
            _service       = service;
            _tenantService = tenantService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private async Task<TenantRole?> GetRoleAsync(Guid tenantId, CancellationToken ct) =>
            await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);

        private static bool CanAccess(TenantRole? role) => role.HasValue;
        private static bool CanManage(TenantRole? role) => role is TenantRole.Owner or TenantRole.Admin;

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<PaymentPeriodSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<PaymentPeriodSummaryDto>>> List(
            Guid tenantId, Guid employeeId, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.ListByEmployeeAsync(tenantId, employeeId, ct));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(PaymentPeriodDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentPeriodDto>> GetById(
            Guid tenantId, Guid employeeId, Guid id, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            var period = await _service.GetByIdAsync(tenantId, id, ct);
            return period == null ? NotFound() : Ok(period);
        }

        [HttpPost("generate")]
        [ProducesResponseType(typeof(PaymentPeriodDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<PaymentPeriodDto>> Generate(
            Guid tenantId, Guid employeeId,
            [FromBody] GeneratePaymentPeriodRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            var actualRequest = request with { EmployeeId = employeeId };
            var period = await _service.GenerateAsync(tenantId, actualRequest, ct);
            return CreatedAtAction(nameof(GetById), new { tenantId, employeeId, id = period.Id }, period);
        }

        [HttpPatch("{id:guid}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStatus(
            Guid tenantId, Guid employeeId, Guid id,
            [FromBody] UpdatePaymentPeriodStatusDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            if (!Enum.IsDefined(typeof(PaymentStatus), request.Status))
                return BadRequest($"Invalid status: {request.Status}.");
            await _service.UpdateStatusAsync(tenantId, id, (PaymentStatus)request.Status, ct);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            Guid tenantId, Guid employeeId, Guid id, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            await _service.DeleteAsync(tenantId, id, ct);
            return NoContent();
        }
    }
}
```

- [ ] **Step 4: Create PaymentsController.cs**

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS_BasePlatform.Application.DTOs.HR;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Enums;
using System.Security.Claims;

namespace SaaS_BasePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}/payments")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _service;
        private readonly ITenantService _tenantService;

        public PaymentsController(IPaymentService service, ITenantService tenantService)
        {
            _service       = service;
            _tenantService = tenantService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private async Task<TenantRole?> GetRoleAsync(Guid tenantId, CancellationToken ct) =>
            await _tenantService.GetUserRoleAsync(tenantId, CurrentUserId, ct);

        private static bool CanAccess(TenantRole? role) => role.HasValue;
        private static bool CanManage(TenantRole? role) => role is TenantRole.Owner or TenantRole.Admin;

        [HttpGet("recent")]
        [ProducesResponseType(typeof(IReadOnlyList<PaymentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<PaymentDto>>> ListRecent(
            Guid tenantId, [FromQuery] int count = 20, CancellationToken ct = default)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.ListRecentAsync(tenantId, count, ct));
        }

        [HttpGet("employee/{employeeId:guid}")]
        [ProducesResponseType(typeof(IReadOnlyList<PaymentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IReadOnlyList<PaymentDto>>> ListByEmployee(
            Guid tenantId, Guid employeeId, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            return Ok(await _service.ListByEmployeeAsync(tenantId, employeeId, ct));
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentDto>> GetById(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanAccess(await GetRoleAsync(tenantId, ct))) return Forbid();
            var payment = await _service.GetByIdAsync(tenantId, id, ct);
            return payment == null ? NotFound() : Ok(payment);
        }

        [HttpPost]
        [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<PaymentDto>> Create(
            Guid tenantId, [FromBody] CreatePaymentRequestDto request, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            var payment = await _service.CreateAsync(tenantId, CurrentUserId, request, ct);
            return CreatedAtAction(nameof(GetById), new { tenantId, id = payment.Id }, payment);
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            Guid tenantId, Guid id, CancellationToken ct)
        {
            if (!CanManage(await GetRoleAsync(tenantId, ct))) return Forbid();
            await _service.DeleteAsync(tenantId, id, ct);
            return NoContent();
        }
    }
}
```

- [ ] **Step 5: Build full solution**

```bash
dotnet build
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 6: Run all tests**

```bash
dotnet test
```

Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add SaaS_BasePlatform.Api/Controllers/EmployeesController.cs \
        SaaS_BasePlatform.Api/Controllers/WorkLogsController.cs \
        SaaS_BasePlatform.Api/Controllers/PaymentPeriodsController.cs \
        SaaS_BasePlatform.Api/Controllers/PaymentsController.cs
git commit -m "feat: add HR controllers (employees, worklogs, payment periods, payments)"
```

---

## Task 12: EF Core Migration

**Files:** Auto-generated under `SaaS_BasePlatform.Infrastructure/Migrations/`

- [ ] **Step 1: Add migration**

```bash
dotnet ef migrations add AddHrModule -p SaaS_BasePlatform.Infrastructure -s SaaS_BasePlatform.Api
```

Expected: Migration file created, e.g. `20260505XXXXXX_AddHrModule.cs`.

- [ ] **Step 2: Verify migration creates the right tables**

Open the generated `..._AddHrModule.cs` and confirm it contains `CreateTable` calls for:
- `Employees`
- `WorkLogs`
- `PaymentPeriods`
- `Payments`

If any are missing, check that the DbSets were added in Task 9 Step 5.

- [ ] **Step 3: Apply migration**

```bash
dotnet ef database update -p SaaS_BasePlatform.Infrastructure -s SaaS_BasePlatform.Api
```

Expected: Database updated successfully.

- [ ] **Step 4: Commit**

```bash
git add SaaS_BasePlatform.Infrastructure/Migrations/
git commit -m "feat: add EF migration AddHrModule"
```

---

## Task 13: Final Verification

- [ ] **Step 1: Full clean build**

```bash
dotnet build --configuration Release
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 2: Run all tests**

```bash
dotnet test
```

Expected: All tests pass. You should see at least 15 tests total (prior + 7 new HR domain tests).

- [ ] **Step 3: Start the API and verify startup**

```bash
dotnet run --project SaaS_BasePlatform.Api
```

Watch for:
- No exceptions during startup
- Migrations applied message

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "feat: complete HR module backend — employees, worklogs, payment periods, payments"
```

---

## Quick Reference: New Endpoints

| Method | URL | Auth | Description |
|--------|-----|------|-------------|
| GET | `api/tenants/{id}/employees` | Any member | List employees |
| GET | `api/tenants/{id}/employees/{eid}` | Any member | Single employee |
| POST | `api/tenants/{id}/employees` | Admin/Owner | Create employee |
| PUT | `api/tenants/{id}/employees/{eid}` | Admin/Owner | Update employee |
| DELETE | `api/tenants/{id}/employees/{eid}` | Admin/Owner | Deactivate employee |
| GET | `api/tenants/{id}/employees/{eid}/worklogs` | Any member | List worklogs (supports ?from=&to=&onlyUnassigned=) |
| GET | `api/tenants/{id}/employees/{eid}/worklogs/{wid}` | Any member | Single worklog |
| POST | `api/tenants/{id}/employees/{eid}/worklogs` | Admin/Owner | Create worklog |
| PUT | `api/tenants/{id}/employees/{eid}/worklogs/{wid}` | Admin/Owner | Update worklog |
| DELETE | `api/tenants/{id}/employees/{eid}/worklogs/{wid}` | Admin/Owner | Delete worklog |
| GET | `api/tenants/{id}/employees/{eid}/payment-periods` | Any member | List payment period summaries |
| GET | `api/tenants/{id}/employees/{eid}/payment-periods/{pid}` | Any member | Period with worklogs |
| POST | `api/tenants/{id}/employees/{eid}/payment-periods/generate` | Admin/Owner | Generate period from worklogs |
| PATCH | `api/tenants/{id}/employees/{eid}/payment-periods/{pid}/status` | Admin/Owner | Update period status |
| DELETE | `api/tenants/{id}/employees/{eid}/payment-periods/{pid}` | Admin/Owner | Delete period (unlocks worklogs) |
| GET | `api/tenants/{id}/payments/recent` | Any member | Recent payments (supports ?count=) |
| GET | `api/tenants/{id}/payments/employee/{eid}` | Any member | Payments for employee |
| GET | `api/tenants/{id}/payments/{pid}` | Any member | Single payment |
| POST | `api/tenants/{id}/payments` | Admin/Owner | Record payment (marks period Paid) |
| DELETE | `api/tenants/{id}/payments/{pid}` | Admin/Owner | Delete payment (reverts period to Pending) |
