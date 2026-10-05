namespace Infrastructure.Persistence.Messages;

public enum ProcessedMessageOutcome
{
    Completed,
    Retried,
    DeadLettered,
    Skipped
}