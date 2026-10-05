using Application.Common;

namespace AiJobPlatform.Api.Infrastructure;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst("sub")?.Value, out var id)
            ? id
            : throw new UnauthorizedAccessException("unauthorized Authentication is required.");
}