using Application.Messaging;

namespace Application.IRepositories;

public interface IProcessedMessageRepository
{
    Task<ProcessedMessage?> FindAsync(Guid messageId, CancellationToken ct);

    void Add(ProcessedMessage message);
}