using FluentValidation;
using PoC.Shared.Models;

namespace PoC.Shared.Validators;

public class MaterialFormulationValidator : AbstractValidator<MaterialFormulation>
{
    public MaterialFormulationValidator()
    {
        RuleFor(x => x.MaterialId)
            .NotEmpty()
            .WithMessage("Material ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Material name is required.")
            .MaximumLength(100)
            .WithMessage("Material name cannot exceed 100 characters.");

        RuleFor(x => x.Formulation)
            .NotEmpty()
            .WithMessage("At least one formulation component is required.");

        RuleForEach(x => x.Formulation).SetValidator(new FormulationComponentValidator());
    }
}

public class FormulationComponentValidator : AbstractValidator<FormulationComponent>
{
    public FormulationComponentValidator()
    {
        RuleFor(x => x.Component)
            .NotEmpty()
            .WithMessage("Component name is required.");

        RuleFor(x => x.Percentage)
            .InclusiveBetween(0, 100)
            .WithMessage("Percentage must be between 0 and 100.");
    }
}
