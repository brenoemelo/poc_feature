using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PoC.Shared.Common;

namespace PoC.Shared.Extensions;

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
            "Error.NotFound" => Results.Json(new ProblemDetails
            {
                Title = "Resource not found",
                Detail = error.Description,
                Status = StatusCodes.Status404NotFound,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4"
            }, statusCode: StatusCodes.Status404NotFound, contentType: "application/problem+json"),
            
            "Error.ConditionNotMet" => Results.Json(new ProblemDetails
            {
                Title = "Conflict",
                Detail = error.Description,
                Status = StatusCodes.Status409Conflict
            }, statusCode: StatusCodes.Status409Conflict, contentType: "application/problem+json"),

            "MissingPrices" or "CurrencyMismatch" or "InvalidMargin" => Results.Json(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = error.Description,
                Status = StatusCodes.Status400BadRequest
            }, statusCode: StatusCodes.Status400BadRequest, contentType: "application/problem+json"),

            _ => Results.Json(new ProblemDetails
            {
                Title = "An error occurred",
                Detail = error.Description,
                Status = StatusCodes.Status500InternalServerError
            }, statusCode: StatusCodes.Status500InternalServerError, contentType: "application/problem+json")
        };
    }
}
