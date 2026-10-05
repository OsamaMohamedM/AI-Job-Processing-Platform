using AiJobPlatform.Application.Jobs;
using Application.Common;
using Application.IRepositories;
using Domain.Jobs.Enums;
using System.Text.Json;

namespace Application.Jobs.Features.GetJobResult;

public sealed class GetJobResultHandler(IJobQueriesRepository queries)
{
    public async Task<JobResultResponse> HandleAsync(Guid userId, Guid jobId, CancellationToken ct)
    {
        var r = await queries.GetResultAsync(userId, jobId, ct) ?? throw GetJobHandler.JobNotFound();

        return r.Status switch
        {
            JobStatus.Completed =>
                new JobResultResponse(jobId, JsonSerializer.Deserialize<JsonElement>(r.ResultJson!)),
            JobStatus.DeadLettered or JobStatus.Failed =>
                throw new ConflictException($"job_failed The job failed: {r.LastError}"),
            _ => throw new ConflictException($"job_not_completed The job is not completed yet (status: {r.Status}).")
        };
    }
}