namespace Domain.Jobs.Enums;

public enum StartAttemptResult
{
    Success,
    LeaseInFlight,
    AttemptsExhausted
}