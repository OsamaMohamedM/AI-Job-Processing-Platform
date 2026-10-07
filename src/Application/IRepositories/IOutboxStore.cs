using Application.Messaging;

namespace Application.IRepositories;

public interface IOutboxStoreRepository
{
    Task<IReadOnlyList<OutboxMessage>> GetDueAsync(int batchSize, DateTime now, CancellationToken ct);
}