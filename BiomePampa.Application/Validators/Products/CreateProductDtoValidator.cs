using BiomePampa.Application.DTOs.Products;
using FluentValidation;

namespace BiomePampa.Application.Validators.Products
{
    public class CreateProductDtoValidator : AbstractValidator<CreateProductDto>
    {
        public CreateProductDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Nome do produto é obrigatório")
                .MinimumLength(3).WithMessage("Nome deve ter no mínimo 3 caracteres")
                .MaximumLength(200).WithMessage("Nome deve ter no máximo 200 caracteres");

            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Descrição é obrigatória")
                .MaximumLength(1000).WithMessage("Descrição deve ter no máximo 1000 caracteres");

            RuleFor(x => x.SKU)
                .NotEmpty().WithMessage("SKU é obrigatório")
                .MaximumLength(50).WithMessage("SKU deve ter no máximo 50 caracteres")
                .Matches(@"^[A-Z0-9-]+$").WithMessage("SKU deve conter apenas letras maiúsculas, números e hífens");

            RuleFor(x => x.Volume)
                .GreaterThan(0).WithMessage("Volume deve ser maior que zero");

            RuleFor(x => x.MinimumStock)
                .GreaterThanOrEqualTo(0).WithMessage("Estoque mínimo não pode ser negativo");

            RuleFor(x => x.MaximumStock)
                .GreaterThan(x => x.MinimumStock).WithMessage("Estoque máximo deve ser maior que o estoque mínimo");

            RuleFor(x => x.UnitPrice)
                .GreaterThan(0).WithMessage("Preço unitário deve ser maior que zero");

            RuleFor(x => x.Barcode)
                .Matches(@"^\d{8,13}$").When(x => !string.IsNullOrEmpty(x.Barcode))
                .WithMessage("Código de barras deve conter entre 8 e 13 dígitos");

            RuleFor(x => x.OliveOilType)
                .IsInEnum().WithMessage("Tipo de azeite inválido");
        }
    }
}
