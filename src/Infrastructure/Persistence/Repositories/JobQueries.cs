using AiJobPlatform.Application.Jobs;
using Application.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class JobQueries(AppDbContext db) : IJobQueriesRepository
{
    public async Task<JobDto?> GetAsync(Guid userId, Guid jobId, CancellationToken ct)
    {
        var r = await db.Jobs.AsNoTracking()
            .Where(j => j.Id == jobId && j.UserId == userId)
            .Select(j => new
            {
                j.Id,
                j.Type,
                j.Status,
                j.AttemptCount,
                j.MaxAttempts,
                j.CreatedAt,
                j.StartedAt,
                j.CompletedAt,
                j.FailedAt,
                j.LastError
            })
            .FirstOrDefaultAsync(ct);
        return r is null ? null : new JobDto(r.Id, r.Type.ToString(), r.Status.ToString(), r.AttemptCount,
            r.MaxAttempts, r.CreatedAt, r.StartedAt, r.CompletedAt, r.FailedAt, r.LastError);
    }

    public Task<JobResultLookup?> GetResultAsync(Guid userId, Guid jobId, CancellationToken ct)
        => db.Jobs.AsNoTracking()
            .Where(j => j.Id == jobId && j.UserId == userId)
            .Select(j => new JobResultLookup(j.Status, j.LastError, j.Result != null ? j.Result.Result : null))
            .FirstOrDefaultAsync(ct);

    public async Task<JobListResponse> ListAsync(Guid userId, JobListFilter filter, CancellationToken ct)
    {
        var q = db.Jobs.AsNoTracking().Where(j => j.UserId == userId);
        if (filter.Status is { } s) q = q.Where(j => j.Status == s);
        if (filter.Type is { } t) q = q.Where(j => j.Type == t);

        var total = await q.CountAsync(ct);
        var ordered = filter.Descending
            ? q.OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id)
            : q.OrderBy(j => j.CreatedAt).ThenBy(j => j.Id);

        var rows = await ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(j => new
            {
                j.Id,
                j.Type,
                j.Status,
                j.AttemptCount,
                j.MaxAttempts,
                j.CreatedAt,
                j.StartedAt,
                j.CompletedAt,
                j.FailedAt,
                j.LastError
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new JobDto(r.Id, r.Type.ToString(), r.Status.ToString(), r.AttemptCount,
            r.MaxAttempts, r.CreatedAt, r.StartedAt, r.CompletedAt, r.FailedAt, r.LastError)).ToList();
        return new JobListResponse(items, filter.Page, filter.PageSize, total);
    }
}