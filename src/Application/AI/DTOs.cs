namespace Application.AI;

public sealed record AiRequest(string Instructions, string UserContent);
public sealed record AiResponse(string Text);

public enum AiErrorKind
{
    Timeout,
    Network,
    RateLimited,
    ServerError,
    InvalidResponse,
    BadRequest,
    Unauthorized,
    UnsupportedJobType,
    Unexpected
}