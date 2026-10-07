using Domain.Common.Entities;

namespace Application.Messaging;

public class OutboxMessage : Entity<Guid>
{
    public string Type { get; private set; } = null!;

    public string Payload { get; private set; } = null!;
    public DateTime AvailableAt { get; private set; }

    public DateTime? PublishedAt { get; private set; }

    public int AttemptCount { get; private set; }

    public string? LastError { get; private set; }

    public OutboxMessage()
    { }

    public static OutboxMessage Create(Guid messageId, string type, string payloadJson, DateTime? availableAt = null)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException("MessageId must be a valid GUID.", nameof(messageId));

        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Message type cannot be empty.", nameof(type));

        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("Payload JSON cannot be empty.", nameof(payloadJson));

        var now = DateTime.UtcNow;

        return new OutboxMessage
        {
            Id = messageId,
            Type = type,
            Payload = payloadJson,
            CreatedAt = now,
            AvailableAt = availableAt ?? now,
            AttemptCount = 0
        };
    }

    public void MarkPublished()
    {
        PublishedAt = DateTime.UtcNow;
        LastError = null;
    }

    public void RecordFailure(string error, DateTime nextAvailableAt)
    {
        AttemptCount++;
        LastError = error;
        AvailableAt = nextAvailableAt;
    }
}