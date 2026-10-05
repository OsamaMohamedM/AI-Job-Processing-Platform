namespace Domain.Jobs.Enums;

public enum JobStatus
{
    Pending,
    Queued,
    Processing,
    Retrying,
    Completed,
    Failed,
    DeadLettered,
}