using Application.IRepositories;
using Application.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class OutboxStoreRepository(AppDbContext db) : IOutboxStoreRepository
{
    public async Task<IReadOnlyList<OutboxMessage>> GetDueAsync(int batchSize, DateTime now, CancellationToken ct)
        => await db.OutboxMessages
            .Where(m => m.PublishedAt == null && m.AvailableAt <= now)
            .OrderBy(m => m.AvailableAt)
            .Take(batchSize)
            .ToListAsync(ct);
}