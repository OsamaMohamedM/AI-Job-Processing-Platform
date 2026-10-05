namespace Infrastructure.Persistence.Messages;

public class ProcessedMessage
{
    public Guid MessageId { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    public ProcessedMessageOutcome Outcome { get; private set; }

    private ProcessedMessage()
    { }

    public static ProcessedMessage Create(Guid messageId, ProcessedMessageOutcome outcome)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException("Message ID must be a valid GUID.", nameof(messageId));

        return new ProcessedMessage
        {
            MessageId = messageId,
            ProcessedAt = DateTime.UtcNow,
            Outcome = outcome
        };
    }
}