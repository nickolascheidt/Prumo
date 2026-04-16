using BiomePampa.Application.DTOs.Employee;
using BiomePampa.Application.Validators.Employee;
using BiomePampa.Domain.Enums;
using FluentValidation.TestHelper;

namespace BiomePampa.Tests.Validators;

public class CreateEmployeeDtoValidatorTests
{
    private readonly CreateEmployeeDtoValidator _validator = new();

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
    public void FullName_WhenTooShort_ShouldHaveError()
    {
        var dto = CreateValidDto() with { FullName = "AB" };

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("12345678901")]
    [InlineData("123")]
    [InlineData("")]
    public void CPF_WhenInvalid_ShouldHaveError(string cpf)
    {
        var dto = CreateValidDto() with { CPF = cpf };

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.CPF);
    }

    [Fact]
    public void CPF_WhenValid_ShouldNotHaveError()
    {
        var dto = CreateValidDto() with { CPF = "52998224725" };

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.CPF);
    }

    [Fact]
    public void Email_WhenInvalid_ShouldHaveError()
    {
        var dto = CreateValidDto() with { Email = "not-an-email" };

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_WhenNull_ShouldNotHaveError()
    {
        var dto = CreateValidDto() with { Email = null };

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Phone_WhenInvalid_ShouldHaveError()
    {
        var dto = CreateValidDto() with { Phone = "123" };

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void HireDate_WhenInFuture_ShouldHaveError()
    {
        var dto = CreateValidDto() with { HireDate = DateTime.Today.AddDays(1) };

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.HireDate);
    }

    [Fact]
    public void HourlyRate_WhenZero_ShouldHaveError()
    {
        var dto = CreateValidDto() with { HourlyRate = 0 };

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.HourlyRate);
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
    public void BankAccount_WhenTransferMethodAndEmpty_ShouldHaveError()
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
    public void BankFields_WhenDinheiroMethod_ShouldNotRequire()
    {
        var dto = CreateValidDto() with
        {
            PreferredPaymentMethod = PaymentMethod.Dinheiro,
            PixKey = null,
            BankAccountNumber = null,
            BankAgency = null
        };

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.PixKey);
        result.ShouldNotHaveValidationErrorFor(x => x.BankAccountNumber);
        result.ShouldNotHaveValidationErrorFor(x => x.BankAgency);
    }
}
