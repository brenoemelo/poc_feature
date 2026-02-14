using FluentValidation.Results;
using PoC.Shared.Http;

namespace PoC.Shared.Validation;

public static class ValidationExtensions
{
    public static ValidationProblemDetails ToProblemDetails(this ValidationResult result)
    {
        var problemDetails = new ValidationProblemDetails();

        foreach (var error in result.Errors)
        {
            if (problemDetails.Errors.ContainsKey(error.PropertyName))
            {
                var existing = problemDetails.Errors[error.PropertyName];
                var updated = new string[existing.Length + 1];
                existing.CopyTo(updated, 0);
                updated[existing.Length] = error.ErrorMessage;
                problemDetails.Errors[error.PropertyName] = updated;
            }
            else
            {
                problemDetails.Errors[error.PropertyName] = new[] { error.ErrorMessage };
            }
        }

        return problemDetails;
    }
}
