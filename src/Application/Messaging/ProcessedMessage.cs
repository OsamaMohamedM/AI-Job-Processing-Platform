namespace Application.Messaging;

public enum ProcessedOutcome
{ Completed, Retried, DeadLettered, Skipped }

public class ProcessedMessage
{
    private ProcessedMessage()
    { }

    public Guid MessageId { get; private set; }
    public DateTime ProcessedAt { get; private set; }
    public ProcessedOutcome Outcome { get; private set; }

    public static ProcessedMessage Create(Guid messageId, ProcessedOutcome outcome, DateTime now)
        => new() { MessageId = messageId, Outcome = outcome, ProcessedAt = now };
}