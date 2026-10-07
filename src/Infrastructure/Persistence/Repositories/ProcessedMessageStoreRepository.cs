using Application.IRepositories;
using Application.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class ProcessedMessageStoreRepository(AppDbContext db) : IProcessedMessageRepository
{
    public Task<ProcessedMessage?> FindAsync(Guid id, CancellationToken ct)
        => db.ProcessedMessages.AsNoTracking().FirstOrDefaultAsync(p => p.MessageId == id, ct);

    public void Add(ProcessedMessage m) => db.ProcessedMessages.Add(m);
}