using AiJobPlatform.Application.Jobs;

namespace Application.IRepositories;

public interface IJobQueriesRepository
{
    Task<JobDto?> GetAsync(Guid userId, Guid jobId, CancellationToken ct);

    Task<JobResultLookup?> GetResultAsync(Guid userId, Guid jobId, CancellationToken ct);

    Task<JobListResponse> ListAsync(Guid userId, JobListFilter filter, CancellationToken ct);
}