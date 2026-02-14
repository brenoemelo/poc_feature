using FluentValidation;
using PoC.Shared.Models;

namespace PoC.Shared.Validators;

public class PopulationRequestValidator : AbstractValidator<PopulationRequest>
{
    public PopulationRequestValidator()
    {
        RuleFor(x => x.Count)
            .GreaterThan(0)
            .WithMessage("Count must be greater than 0.")
            .LessThanOrEqualTo(100000)
            .WithMessage("Count exceeds limit of 100,000.");

        RuleFor(x => x.Target)
            .NotEmpty()
            .WithMessage("Target is required.");
    }
}
