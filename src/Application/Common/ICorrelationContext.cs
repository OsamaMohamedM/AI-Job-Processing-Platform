namespace Application.Common;

public interface ICorrelationContext
{
    string CorrelationId { get; }
}