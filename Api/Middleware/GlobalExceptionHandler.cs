using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Middleware;

public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    ProblemDetailsFactory problemDetailsFactory,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ValidationException validation)
        {
            return await WriteValidationProblemAsync(httpContext, validation);
        }

        logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected server error has occurred",
            Detail = "Please try again later or contact your administrator"
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    /// <summary>
    /// The mediator's validation behavior throws when a command or query is invalid. The answer is the same
    /// 400 ValidationProblemDetails MVC gives for an invalid model: errors keyed by property name.
    /// </summary>
    private async ValueTask<bool> WriteValidationProblemAsync(HttpContext httpContext, ValidationException validation)
    {
        logger.LogInformation("Validation failed: {Errors}", validation.Errors.Select(e => e.PropertyName));

        var modelState = new ModelStateDictionary();
        foreach (var error in validation.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        var problem = problemDetailsFactory.CreateValidationProblemDetails(
            httpContext, modelState, StatusCodes.Status400BadRequest);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = validation
        });
    }
}
