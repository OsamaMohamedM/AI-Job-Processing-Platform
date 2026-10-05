using Application.Common;
using Application.IRepositories;
using Domain.Jobs.Enums;
using System.Text.Json;

namespace AiJobPlatform.Application.Jobs;

public sealed class GetJobHandler(IJobQueriesRepository queries)
{
    public async Task<JobDto> HandleAsync(Guid userId, Guid jobId, CancellationToken ct)
        => await queries.GetAsync(userId, jobId, ct) ?? throw JobNotFound();

    internal static NotFoundException JobNotFound() => new("job_not_found", "Job not found.");
}



