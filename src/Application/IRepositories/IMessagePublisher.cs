using Application.Messaging;

namespace Application.IRepositories;

public interface IMessagePublisher
{
    Task PublishAsync(OutboundMessage message, CancellationToken ct);
}