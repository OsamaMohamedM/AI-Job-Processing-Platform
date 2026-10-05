using Application.Common;

namespace API.Infrastructure
{
    public class CorrelationContext(IHttpContextAccessor accessor) : ICorrelationContext
    {
        public string CorrelationId =>
                accessor.HttpContext?.Items["CorrelationId"] as string ?? Guid.NewGuid().ToString("N");
    }
}