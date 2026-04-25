using SaaS_BasePlatform.Application.DTOs.Employee;
using FluentValidation;

namespace SaaS_BasePlatform.Application.Validators.Employee
{
    public class CreateEmployeeDtoValidator : AbstractValidator<CreateEmployeeDto>
    {
        public CreateEmployeeDtoValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Nome completo é obrigatório")
                .MinimumLength(3).WithMessage("Nome deve ter no mínimo 3 caracteres")
                .MaximumLength(200).WithMessage("Nome deve ter no máximo 200 caracteres");

            RuleFor(x => x.CPF)
                .NotEmpty().WithMessage("CPF é obrigatório")
                .Must(BeValidCPF).WithMessage("CPF inválido");

            RuleFor(x => x.Email)
                .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
                .WithMessage("Email inválido");

            RuleFor(x => x.Phone)
                .Matches(@"^\d{10,11}$").When(x => !string.IsNullOrEmpty(x.Phone))
                .WithMessage("Telefone deve conter 10 ou 11 dígitos");

            RuleFor(x => x.HireDate)
                .NotEmpty().WithMessage("Data de contratação é obrigatória")
                .LessThanOrEqualTo(DateTime.Today).WithMessage("Data de contratação não pode ser futura");

            RuleFor(x => x.HourlyRate)
                .GreaterThan(0).WithMessage("Valor da hora deve ser maior que zero");

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

        private bool BeValidCPF(string cpf)
        {
            if (string.IsNullOrWhiteSpace(cpf))
                return false;

            // Remove caracteres não numéricos
            cpf = new string(cpf.Where(char.IsDigit).ToArray());

            if (cpf.Length != 11)
                return false;

            // Verifica se todos os dígitos são iguais
            if (cpf.Distinct().Count() == 1)
                return false;

            // Validação do primeiro dígito verificador
            int[] multiplicador1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            int soma = 0;
            for (int i = 0; i < 9; i++)
                soma += int.Parse(cpf[i].ToString()) * multiplicador1[i];

            int resto = soma % 11;
            if (resto < 2)
                resto = 0;
            else
                resto = 11 - resto;

            if (resto != int.Parse(cpf[9].ToString()))
                return false;

            // Validação do segundo dígito verificador
            int[] multiplicador2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            soma = 0;
            for (int i = 0; i < 10; i++)
                soma += int.Parse(cpf[i].ToString()) * multiplicador2[i];

            resto = soma % 11;
            if (resto < 2)
                resto = 0;
            else
                resto = 11 - resto;

            return resto == int.Parse(cpf[10].ToString());
        }
    }
}
