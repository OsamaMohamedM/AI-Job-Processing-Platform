namespace Application.AI;

public static class AiErrorKindExtensions
{
    public static bool IsRetryable(this AiErrorKind kind) => kind is
        AiErrorKind.Timeout or AiErrorKind.Network or AiErrorKind.RateLimited or
        AiErrorKind.ServerError or AiErrorKind.InvalidResponse;
}