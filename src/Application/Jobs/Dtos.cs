using Domain.Jobs.Enums;
using System.Text.Json;

namespace AiJobPlatform.Application.Jobs;

public sealed record CreateJobRequest(string Type, JsonElement Input);
public sealed record CreateJobResponse(Guid JobId, string Status);

public sealed record JobDto(Guid Id, string Type, string Status, int AttemptCount, int MaxAttempts,
                            DateTime CreatedAt, DateTime? StartedAt, DateTime? CompletedAt,
                            DateTime? FailedAt, string? LastError);
public sealed record JobListResponse(IReadOnlyList<JobDto> Items, int Page, int PageSize, int TotalCount);
public sealed record JobResultResponse(Guid JobId, JsonElement Result);

public sealed record ListJobsQuery(string? Status = null, string? Type = null,
                                   int Page = 1, int PageSize = 20, string Order = "desc");
public sealed record JobListFilter(JobStatus? Status, JobType? Type, int Page, int PageSize, bool Descending);
public sealed record JobResultLookup(JobStatus Status, string? LastError, string? ResultJson);

public static class EnumParser
{
    public static bool TryParse<T>(string? value, out T result) where T : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        foreach (var name in Enum.GetNames<T>())
            if (string.Equals(name, value.Trim(), StringComparison.OrdinalIgnoreCase))
            { result = Enum.Parse<T>(name); return true; }
        return false;
    }
}