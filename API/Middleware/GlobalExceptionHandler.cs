using Application.Common;
using Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

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
        var traceId = httpContext.Items["CorrelationId"]?.ToString()
            ?? httpContext.TraceIdentifier;

        var (statusCode, code, message, errors) = MapException(exception);

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(_logger, exception);
        }
        else
        {
            LogHandledException(_logger, statusCode, code, exception);
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsJsonAsync(
            new ApiError(code, message, traceId, errors),
            cancellationToken);

        return true;
    }

    private static (
        int StatusCode,
        string Code,
        string Message,
        IReadOnlyDictionary<string, string[]>? Errors) MapException(Exception exception)
    {
        return exception switch
        {
            Application.Common.ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                "validation_failed",
                validationException.Message,
                new Dictionary<string, string[]>(validationException.Errors)),

            BadHttpRequestException badRequestException
                when badRequestException.StatusCode == StatusCodes.Status413PayloadTooLarge => (
                    StatusCodes.Status413PayloadTooLarge,
                    "invalid_request",
                    "The request body is too large.",
                    null),

            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "invalid_request",
                "The request payload is invalid or malformed.",
                null),

            UnauthorizedException unauthorizedException => (
                StatusCodes.Status401Unauthorized,
                "invalid_credentials",
                unauthorizedException.Message,
                null),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "unauthorized",
                "You are not authorized to access this resource.",
                null),

            NotFoundException notFoundException => (
                StatusCodes.Status404NotFound,
                "not_found",
                notFoundException.Message,
                null),

            ConflictException conflictException => (
                StatusCodes.Status409Conflict,
                "conflict",
                conflictException.Message,
                null),

            DomainException domainException => (
                StatusCodes.Status409Conflict,
                "invalid_operation",
                domainException.Message,
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "An unexpected error occurred.",
                null)
        };
    }

    private static readonly Action<ILogger, Exception?> LogUnhandledException =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1, "UnhandledException"),
            "UnhandledException");

    private static readonly Action<ILogger, int, string, Exception?> LogHandledException =
        LoggerMessage.Define<int, string>(
            LogLevel.Warning,
            new EventId(2, "HandledException"),
            "HandledException {StatusCode} {ErrorCode}");
}
