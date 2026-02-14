using FluentValidation;
using PoC.Shared.Models;

namespace PoC.Shared.Validators;

public class ComponentPriceRequestValidator : AbstractValidator<ComponentPriceRequest>
{
    public ComponentPriceRequestValidator()
    {
        RuleFor(x => x.ComponentName)
            .NotEmpty().WithMessage("Component name is required")
            .MaximumLength(100).WithMessage("Component name must not exceed 100 characters");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0).WithMessage("Unit price must be greater than zero");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Unit is required")
            .MaximumLength(20).WithMessage("Unit must not exceed 20 characters");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be a 3-letter ISO code (e.g., USD, BRL)");
    }
}

public class CostCalculationRequestValidator : AbstractValidator<CostCalculationRequest>
{
    public CostCalculationRequestValidator()
    {
        RuleFor(x => x.MaterialId)
            .NotEmpty().WithMessage("Material ID is required");

        RuleFor(x => x.Formulation)
            .NotEmpty().WithMessage("Formulation cannot be empty")
            .Must(HaveValidPercentageSum).WithMessage("Formulation percentages must sum to 100%");

        RuleForEach(x => x.Formulation).ChildRules(formulation =>
        {
            formulation.RuleFor(f => f.Component)
                .NotEmpty().WithMessage("Component name is required");

            formulation.RuleFor(f => f.Percentage)
                .GreaterThan(0).WithMessage("Percentage must be greater than zero")
                .LessThanOrEqualTo(100).WithMessage("Percentage must not exceed 100");
        });

        RuleFor(x => x.DesiredMarginPercent)
            .GreaterThan(0).When(x => x.DesiredMarginPercent.HasValue)
            .WithMessage("Margin must be greater than zero if provided")
            .LessThan(100).When(x => x.DesiredMarginPercent.HasValue)
            .WithMessage("Margin must be less than 100%");
    }

    private bool HaveValidPercentageSum(List<FormulationInput> formulation)
    {
        if (formulation == null || formulation.Count == 0) return false;
        
        var sum = formulation.Sum(f => f.Percentage);
        // Allow small floating point tolerance
        return Math.Abs(sum - 100.0) < 0.01;
    }
}
