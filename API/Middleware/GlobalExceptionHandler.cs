using Application.Common;
using Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items["CorrelationId"]?.ToString()
                            ?? httpContext.TraceIdentifier;

        var (statusCode, title, detail, errors) = MapException(exception);

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred. CorrelationId: {CorrelationId}", correlationId);
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception mapped to Status {StatusCode}. CorrelationId: {CorrelationId}", statusCode, correlationId);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["correlationId"] = correlationId;

        if (errors != null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title, string Detail, object? Errors) MapException(Exception exception)
    {
        return exception switch
        {
            ValidationException valEx => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                valEx.Message,
                valEx.Errors
            ),

            BadHttpRequestException badReqEx when badReqEx.StatusCode == StatusCodes.Status400BadRequest => (
                StatusCodes.Status400BadRequest,
                "Malformed Request",
                "The request payload is invalid or malformed.",
                null
            ),

            BadHttpRequestException badReqEx when badReqEx.StatusCode == StatusCodes.Status413PayloadTooLarge => (
                StatusCodes.Status413PayloadTooLarge,
                "Payload Too Large",
                "The request entity exceeds the maximum allowed limit.",
                null
            ),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "You are not authorized to access this resource.",
                null
            ),

            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                notFoundEx.Message,
                null
            ),

            ConflictException conflictEx => (
                StatusCodes.Status409Conflict,
                "Conflict",
                conflictEx.Message,
                null
            ),

            DomainException domainEx => (
                StatusCodes.Status409Conflict,
                "Domain Rule Violation",
                domainEx.Message,
                null
            ),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred. Please use the correlation ID when contacting support.",
                null
            )
        };
    }
}