namespace Application.Messaging;

public sealed record JobQueuedMessage(
    int SchemaVersion, Guid MessageId, Guid JobId, string JobType,
    string CorrelationId, string Reason, DateTime OccurredAt)
{
    public const string OutboxType = "JobQueued";
    public const int CurrentSchemaVersion = 1;

    public static JobQueuedMessage Create(Guid jobId, string jobType, string correlationId, string reason)
        => new(CurrentSchemaVersion, Guid.CreateVersion7(), jobId, jobType, correlationId, reason, DateTime.UtcNow);
}