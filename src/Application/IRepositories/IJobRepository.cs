using Domain.Jobs.Entities;

namespace Application.IRepositories;

public interface IJobRepository
{
    void Add(Job job);

    Task<Job?> GetForProcessingAsync(Guid id, CancellationToken ct);

    Task<bool> TryMarkQueuedAsync(Guid jobId, CancellationToken ct);
}