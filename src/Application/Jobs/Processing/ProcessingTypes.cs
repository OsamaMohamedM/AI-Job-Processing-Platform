namespace Application.Jobs.Processing;

using Application.AI;
using System.ComponentModel.DataAnnotations;

public enum MessageDisposition
{ Ack, RejectToDlq }

public sealed record ConsumedMessage(Guid MessageId, Guid JobId, string JobType, string CorrelationId, string Reason);

public sealed class ProcessingOptions
{
    [Range(30, 3600)]
    public int LeaseSeconds { get; set; } = 120;

    [Range(1, 64)]
    public int MaxConcurrentJobs { get; set; } = 4;

    [Range(1, 300)]
    public int RequeueDelaySeconds { get; set; } = 5;
}

public static class JobFailureMessages
{
    public static string For(AiErrorKind kind) => kind switch
    {
        AiErrorKind.Timeout => "AI provider timed out.",
        AiErrorKind.Network => "AI provider could not be reached.",
        AiErrorKind.RateLimited => "AI provider rate limit reached.",
        AiErrorKind.ServerError => "AI provider returned a server error.",
        AiErrorKind.InvalidResponse => "AI response failed validation.",
        AiErrorKind.BadRequest => "AI provider rejected the request.",
        AiErrorKind.Unauthorized => "AI provider is not configured correctly.",
        AiErrorKind.UnsupportedJobType => "Unsupported job type.",
        _ => "Unexpected processing error."
    };
}