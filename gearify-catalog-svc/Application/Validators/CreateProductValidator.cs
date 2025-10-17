using FluentValidation;
using Gearify.CatalogService.Application.Commands;

namespace Gearify.CatalogService.Application.Validators;

public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("TenantId is required");
        RuleFor(x => x.Sku).NotEmpty().WithMessage("SKU is required");
        RuleFor(x => x.Name).NotEmpty().MinimumLength(3).MaximumLength(200).WithMessage("Name must be between 3 and 200 characters");
        RuleFor(x => x.Category).NotEmpty().WithMessage("Category is required");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than 0");
        RuleFor(x => x.CompareAtPrice).GreaterThanOrEqualTo(x => x.Price).When(x => x.CompareAtPrice > 0)
            .WithMessage("Compare at price must be greater than or equal to price");
    }
}
