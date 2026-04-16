using BiomePampa.Application.DTOs.Employee;
using BiomePampa.Application.Validators.Employee;
using BiomePampa.Domain.Enums;
using FluentValidation.TestHelper;

namespace BiomePampa.Tests.Validators;

public class UpdateEmployeeDtoValidatorTests
{
    private readonly UpdateEmployeeDtoValidator _validator = new();

    private static UpdateEmployeeDto CreateValidDto() => new(
        FullName: "João da Silva",
        Phone: "51999998888",
        Email: "joao@email.com",
        IsActive: true,
        ContractType: ContractType.CLT,
        HourlyRate: 25.00m,
        PreferredPaymentMethod: PaymentMethod.Pix,
        PixKey: "joao@email.com",
        BankName: null,
        BankAccountNumber: null,
        BankAgency: null,
        HasSignedContract: true,
        TerminationDate: null
    );

    [Fact]
    public void ValidDto_ShouldPassValidation()
    {
        var dto = CreateValidDto();

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void FullName_WhenEmpty_ShouldHaveError(string? name)
    {
        var dto = CreateValidDto() with { FullName = name! };

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void HourlyRate_WhenNegative_ShouldHaveError()
    {
        var dto = CreateValidDto() with { HourlyRate = -5m };

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.HourlyRate);
    }

    [Fact]
    public void TerminationDate_WhenInFuture_ShouldHaveError()
    {
        var dto = CreateValidDto() with { TerminationDate = DateTime.Today.AddDays(10) };

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.TerminationDate);
    }

    [Fact]
    public void TerminationDate_WhenNull_ShouldNotHaveError()
    {
        var dto = CreateValidDto() with { TerminationDate = null };

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.TerminationDate);
    }

    [Fact]
    public void TerminationDate_WhenInPast_ShouldNotHaveError()
    {
        var dto = CreateValidDto() with { TerminationDate = DateTime.Today.AddDays(-5) };

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.TerminationDate);
    }

    [Fact]
    public void PixKey_WhenPixMethodAndEmpty_ShouldHaveError()
    {
        var dto = CreateValidDto() with
        {
            PreferredPaymentMethod = PaymentMethod.Pix,
            PixKey = null
        };

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.PixKey);
    }

    [Fact]
    public void BankFields_WhenTransferMethodAndEmpty_ShouldHaveError()
    {
        var dto = CreateValidDto() with
        {
            PreferredPaymentMethod = PaymentMethod.TransferenciaBancaria,
            PixKey = null,
            BankAccountNumber = null,
            BankAgency = null
        };

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.BankAccountNumber);
        result.ShouldHaveValidationErrorFor(x => x.BankAgency);
    }

    [Fact]
    public void Email_WhenInvalid_ShouldHaveError()
    {
        var dto = CreateValidDto() with { Email = "invalid" };

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Phone_WhenInvalidFormat_ShouldHaveError()
    {
        var dto = CreateValidDto() with { Phone = "abc" };

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }
}
