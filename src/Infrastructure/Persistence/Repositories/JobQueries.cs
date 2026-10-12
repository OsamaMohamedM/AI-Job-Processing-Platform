using AiJobPlatform.Application.Jobs;
using Application.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class JobQueries(AppDbContext db) : IJobQueriesRepository
{
    public async Task<JobDto?> GetAsync(Guid userId, Guid jobId, CancellationToken ct)
    {
        return await db.Jobs.AsNoTracking()
            .Where(j => j.Id == jobId && j.UserId == userId)
            .Select(j => new JobDto(
                j.Id,
                j.Type.ToString(),
                j.Status.ToString(),
                j.AttemptCount,
                j.MaxAttempts,
                j.CreatedAt,
                j.StartedAt,
                j.CompletedAt,
                j.FailedAt,
                j.LastError))
            .FirstOrDefaultAsync(ct);
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
        if (total == 0)
        {
            return new JobListResponse([], filter.Page, filter.PageSize, 0);
        }

        var ordered = filter.Descending
            ? q.OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id)
            : q.OrderBy(j => j.CreatedAt).ThenBy(j => j.Id);

        var items = await ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(j => new JobDto(
                j.Id,
                j.Type.ToString(),
                j.Status.ToString(),
                j.AttemptCount,
                j.MaxAttempts,
                j.CreatedAt,
                j.StartedAt,
                j.CompletedAt,
                j.FailedAt,
                j.LastError))
            .ToListAsync(ct);

        return new JobListResponse(items, filter.Page, filter.PageSize, total);
    }
}