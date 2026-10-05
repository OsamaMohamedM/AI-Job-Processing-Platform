using Domain.Common.Entities;
using Domain.Jobs.Entities;
using Domain.Jobs.Enums;

namespace Domain.Jobs.Entites;

public class JobAttempt : Entity<Guid>
{
    public Guid JobId { get; private set; }

    public int AttemptNumber { get; private set; }

    public DateTime StartedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public JobAttemptStatus Status { get; private set; }

    public string? ErrorMessage { get; private set; }

    public virtual Job Job { get; private set; } = null!;

    private JobAttempt()
    { }

    public static JobAttempt Start(Guid jobId, int attemptNumber)
    {
        if (attemptNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber), "Attempt number must be greater than or equal to 1.");
        }

        return new JobAttempt
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            AttemptNumber = attemptNumber,
            StartedAt = DateTime.UtcNow,
            Status = JobAttemptStatus.Started
        };
    }

    public void Succeed(string Id)
    {
        EnsureStatus(JobAttemptStatus.Started);
        MarkModified(Id);
        Status = JobAttemptStatus.Succeeded;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string errorMessage, string Id)
    {
        EnsureStatus(JobAttemptStatus.Started);
        MarkModified(Id);

        Status = JobAttemptStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }

    public void Abandon(string reason)
    {
        EnsureStatus(JobAttemptStatus.Started);

        Status = JobAttemptStatus.Abandoned;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = reason;
    }

    private void EnsureStatus(JobAttemptStatus expectedStatus)
    {
        if (Status != expectedStatus)
        {
            throw new InvalidOperationException($"Cannot transition attempt {Id} from state '{Status}'. Expected state: '{expectedStatus}'.");
        }
    }
}