using AiJobPlatform.Application.Jobs;
using Application.IRepositories;
using Domain.Jobs.Enums;

namespace Application.Jobs.Features.ListJobs;

public sealed class ListJobsHandler(IJobQueriesRepository queries)
{
    public Task<JobListResponse> HandleAsync(Guid userId, ListJobsQuery q, CancellationToken ct)
    {
        JobStatus? status = EnumParser.TryParse<JobStatus>(q.Status, out var s) ? s : null;
        JobType? type = EnumParser.TryParse<JobType>(q.Type, out var t) ? t : null;
        var filter = new JobListFilter(status, type, q.Page, q.PageSize,
                                       Descending: !string.Equals(q.Order, "asc", StringComparison.OrdinalIgnoreCase));
        return queries.ListAsync(userId, filter, ct);
    }
}