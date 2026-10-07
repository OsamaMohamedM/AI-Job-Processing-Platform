using Application.AI;
using Domain.Jobs.Enums;

namespace Application.Common;

public abstract class GenralException : Exception
{
    public GenralException(string message) : base(message)
    {
    }
}

public class NotFoundException : GenralException
{
    public NotFoundException(string resourceName, object key)
        : base($"Entity \"{resourceName}\" ({key}) was not found.") { }
}

public class ConflictException : GenralException
{
    public ConflictException(string message) : base(message)
    {
    }
}

public class UnauthorizedException : GenralException
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}

public class ValidationException : GenralException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation failures have occurred.")
    {
        Errors = errors;
    }
}

public sealed class UnsupportedJobTypeException(JobType type)
    : Exception($"No handler registered for job type {type}.");

public class AiProviderException(AiErrorKind kind, string message, int? statusCode = null,
                                        TimeSpan? retryAfter = null, Exception? inner = null)
    : Exception(message, inner)
{
    public AiErrorKind Kind { get; } = kind;
    public int? StatusCode { get; } = statusCode;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

public sealed class InvalidAiResponseException(string message) : Exception(message);

public sealed class UniqueConstraintViolationException(string message, Exception inner) : Exception(message, inner);

public sealed class ConcurrencyConflictException(string message, Exception inner) : Exception(message, inner);