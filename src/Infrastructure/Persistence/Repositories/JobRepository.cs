using Application.IRepositories;
using Domain.Jobs.Entities;
using Domain.Jobs.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class JobRepository(AppDbContext db) : IJobRepository
{
    public void Add(Job job) => db.Jobs.Add(job);

    public Task<Job?> GetForProcessingAsync(Guid id, CancellationToken ct)
        => db.Jobs.Include(j => j.Attempts).FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<bool> TryMarkQueuedAsync(Guid jobId, CancellationToken ct)
    => await db.Jobs.Where(j => j.Id == jobId && j.Status == JobStatus.Pending)
           .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, JobStatus.Queued), ct) == 1;

    public async Task<int> TryMarkQueuedBatchAsync(IReadOnlyList<Guid> jobIds, CancellationToken ct)
    {
        if (jobIds.Count == 0) return 0;
        return await db.Jobs.Where(j => jobIds.Contains(j.Id) && j.Status == JobStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, JobStatus.Queued), ct);
    }
}