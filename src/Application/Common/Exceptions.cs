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