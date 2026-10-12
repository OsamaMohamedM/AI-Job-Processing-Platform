using Domain.Common;
using Domain.Common.Entities;
using Domain.Identity;
using Domain.Jobs.Entites;
using Domain.Jobs.Enums;

namespace Domain.Jobs.Entities;

public class Job : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public JobType Type { get; private set; }
    public JobStatus Status { get; private set; }
    public string Input { get; private set; } = null!;
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    public virtual User User { get; private set; } = null!;

    private readonly List<JobAttempt> _attempts = new();
    public virtual IReadOnlyCollection<JobAttempt> Attempts => _attempts.AsReadOnly();

    public virtual JobResult? Result { get; private set; }

    private Job()
    { }

    public static Job Create(Guid userId, JobType type, string input, int maxAttempts)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID must be valid.", nameof(userId));

        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input JSON cannot be empty.", nameof(input));

        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "MaxAttempts must be at least 1.");

        var job = new Job
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Status = JobStatus.Pending,
            Input = input,
            AttemptCount = 0,
            MaxAttempts = maxAttempts,
            RowVersion = Array.Empty<byte>(),
            CreatedBy = userId.ToString()
        };

        return job;
    }

    public void MarkQueued(string modifiedBy)
    {
        EnsureNotTerminal();

        if (Status != JobStatus.Pending)
        {
            return;
        }

        Status = JobStatus.Queued;
        MarkModified(modifiedBy);
    }

    public StartAttemptResult StartAttempt(
      DateTime now,
      TimeSpan leaseDuration,
      string modifiedBy)
    {
        EnsureNotTerminal();

        if (Status == JobStatus.Processing)
        {
            if (LeaseExpiresAt.HasValue && LeaseExpiresAt.Value > now)
            {
                return StartAttemptResult.InFlight;
            }

            var openAttempt = GetActiveAttempt();
            openAttempt?.Abandon("Lease expired while processing.");

            if (AttemptCount >= MaxAttempts)
            {
                DeadLetter("Maximum number of attempts has been exhausted.", modifiedBy);

                return StartAttemptResult.AttemptsExhausted;
            }
        }

        if (AttemptCount >= MaxAttempts)
        {
            return StartAttemptResult.AttemptsExhausted;
        }

        EnsureStatus(
            JobStatus.Queued,
            JobStatus.Retrying);

        AttemptCount++;

        Status = JobStatus.Processing;
        LeaseExpiresAt = now.Add(leaseDuration);

        StartedAt ??= now;

        var attempt = JobAttempt.Start(
            Id,
            AttemptCount
            );

        _attempts.Add(attempt);

        MarkModified(modifiedBy);

        return StartAttemptResult.Started;
    }

    public void Complete(string resultJson, string modifiedBy)
    {
        EnsureStatus(JobStatus.Processing);

        if (string.IsNullOrWhiteSpace(resultJson))
            throw new ArgumentException(
                "Result JSON cannot be empty.",
                nameof(resultJson));

        var activeAttempt = GetActiveAttempt()
            ?? throw new DomainException(
                $"Job {Id} is processing but has no active attempt.");

        activeAttempt.Succeed(modifiedBy);

        Result = JobResult.Create(Id, resultJson);

        Status = JobStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        LeaseExpiresAt = null;
        LastError = null;

        MarkModified(modifiedBy);
    }

    public void ScheduleRetry(string errorMessage, string modifiedBy)
    {
        EnsureStatus(JobStatus.Processing);

        if (!CanRetry())
        {
            DeadLetter(errorMessage, modifiedBy);
            return;
        }

        var activeAttempt = GetActiveAttempt()
            ?? throw new DomainException(
                $"Job {Id} is processing but has no active attempt.");

        activeAttempt.Fail(errorMessage, modifiedBy);

        LastError = errorMessage;
        LeaseExpiresAt = null;
        Status = JobStatus.Retrying;

        MarkModified(modifiedBy);
    }

    public void DeadLetter(string reason, string modifiedBy)
    {
        EnsureNotTerminal();

        var activeAttempt = GetActiveAttempt();
        activeAttempt?.Fail(reason, modifiedBy);

        Status = JobStatus.Failed;
        FailedAt = DateTime.UtcNow;
        LastError = reason;
        LeaseExpiresAt = null;

        Status = JobStatus.DeadLettered;

        MarkModified(modifiedBy);
    }

    public bool CanRetry()
    {
        return Status != JobStatus.Completed
            && Status != JobStatus.DeadLettered
            && AttemptCount < MaxAttempts;
    }

    private JobAttempt? GetActiveAttempt()
    {
        return _attempts.FirstOrDefault(a => a.Status == JobAttemptStatus.Started);
    }

    private void EnsureNotTerminal()
    {
        if (Status == JobStatus.Completed || Status == JobStatus.DeadLettered)
        {
            throw new DomainException($"Job {Id} is in terminal state '{Status}' and cannot be modified.");
        }
    }

    private void EnsureStatus(params JobStatus[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(Status))
        {
            throw new DomainException(
                $"Cannot transition job {Id} from state '{Status}'. Allowed states: {string.Join(", ", allowedStatuses)}."
            );
        }
    }
}