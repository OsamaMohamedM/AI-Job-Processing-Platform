using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace API.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderKey = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeaderKey, out var correlationId) ||
            string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        var corrId = correlationId.ToString();
        var traceId = context.TraceIdentifier;

        context.Response.Headers[CorrelationIdHeaderKey] = corrId;
        context.Items["CorrelationId"] = corrId;

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", corrId))
        {
            LogCorrelationContextEstablished(_logger, corrId, traceId, null);

            var startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                await _next(context);
            }
            finally
            {
                var durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                var route = context.Request.Path.Value ?? "/";
                var statusCode = context.Response.StatusCode;
                LogHttpRequestCompleted(_logger, traceId, corrId, route, statusCode, durationMs, null);
            }
        }
    }

    private static readonly Action<ILogger, string, string, Exception?> LogCorrelationContextEstablished =
        LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(101, "CorrelationContextEstablished"),
            "CorrelationContextEstablished {CorrelationId} {TraceId}");

    private static readonly Action<ILogger, string, string, string, int, double, Exception?> LogHttpRequestCompleted =
        LoggerMessage.Define<string, string, string, int, double>(
            LogLevel.Information,
            new EventId(102, "HttpRequestCompleted"),
            "HttpRequestCompleted {TraceId} {CorrelationId} {Route} {StatusCode} {DurationMs}");
}