namespace Domain.Jobs.Enums;

public enum StartAttemptResult
{
    Started,
    InFlight,
    AlreadyFinished,
    AttemptsExhausted,
    Success
}