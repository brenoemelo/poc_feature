using Microsoft.AspNetCore.Http;
using PoC.Shared.Common;

namespace PoC.Shared.API;

public static class ResultExtensions
{
    public static IResult ToProblem(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert success result to problem.");
        }

        var error = result.Error;

        return error.Code switch
        {
            "Error.NotFound" => Results.Problem(
                title: "Resource not found",
                detail: error.Description,
                statusCode: StatusCodes.Status404NotFound),
            
            "MissingPrices" or "CurrencyMismatch" or "InvalidMargin" or "Error.ConditionNotMet" => Results.Problem(
                title: "Validation Error",
                detail: error.Description,
                statusCode: StatusCodes.Status400BadRequest),

            _ => Results.Problem(
                title: "An error occurred",
                detail: error.Description,
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
