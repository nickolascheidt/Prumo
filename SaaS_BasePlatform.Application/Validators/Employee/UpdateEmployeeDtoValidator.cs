using SaaS_BasePlatform.Application.DTOs.Employee;
using FluentValidation;

namespace SaaS_BasePlatform.Application.Validators.Employee
{
    public class UpdateEmployeeDtoValidator : AbstractValidator<UpdateEmployeeDto>
    {
        public UpdateEmployeeDtoValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Nome completo é obrigatório")
                .MinimumLength(3).WithMessage("Nome deve ter no mínimo 3 caracteres")
                .MaximumLength(200).WithMessage("Nome deve ter no máximo 200 caracteres");

            RuleFor(x => x.Email)
                .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
                .WithMessage("Email inválido");

            RuleFor(x => x.Phone)
                .Matches(@"^\d{10,11}$").When(x => !string.IsNullOrEmpty(x.Phone))
                .WithMessage("Telefone deve conter 10 ou 11 dígitos");

            RuleFor(x => x.HourlyRate)
                .GreaterThan(0).WithMessage("Valor da hora deve ser maior que zero");

            RuleFor(x => x.TerminationDate)
                .LessThanOrEqualTo(DateTime.Today).When(x => x.TerminationDate.HasValue)
                .WithMessage("Data de demissão não pode ser futura");

            RuleFor(x => x.PixKey)
                .NotEmpty().When(x => x.PreferredPaymentMethod == Domain.Enums.PaymentMethod.Pix)
                .WithMessage("Chave PIX é obrigatória quando o método de pagamento é PIX");

            RuleFor(x => x.BankAccountNumber)
                .NotEmpty().When(x => x.PreferredPaymentMethod == Domain.Enums.PaymentMethod.TransferenciaBancaria)
                .WithMessage("Conta bancária é obrigatória quando o método de pagamento é transferência");

            RuleFor(x => x.BankAgency)
                .NotEmpty().When(x => x.PreferredPaymentMethod == Domain.Enums.PaymentMethod.TransferenciaBancaria)
                .WithMessage("Agência bancária é obrigatória quando o método de pagamento é transferência");
        }
    }
}
