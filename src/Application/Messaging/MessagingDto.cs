namespace Application.Messaging;

public sealed record OutboundMessage(Guid MessageId, string Type, string Body, string? CorrelationId);