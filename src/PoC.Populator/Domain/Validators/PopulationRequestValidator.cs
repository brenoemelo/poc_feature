using FluentValidation;
using PoC.Populator.Domain.Models;

namespace PoC.Populator.Domain.Validators;

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

        RuleFor(x => x.MinComponents)
            .GreaterThanOrEqualTo(1)
            .WithMessage("MinComponents must be at least 1.")
            .LessThanOrEqualTo(1000)
            .WithMessage("MinComponents must be at most 1000.")
            .When(x => x.MinComponents.HasValue);

        RuleFor(x => x.MaxComponents)
            .GreaterThanOrEqualTo(1)
            .WithMessage("MaxComponents must be at least 1.")
            .LessThanOrEqualTo(1000)
            .WithMessage("MaxComponents must be at most 1000.")
            .GreaterThanOrEqualTo(x => x.MinComponents ?? 1)
            .WithMessage("MaxComponents must be greater than or equal to MinComponents.")
            .When(x => x.MaxComponents.HasValue);
    }
}
