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

public class ValidationException : GenralException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation failures have occurred.")
    {
        Errors = errors;
    }
}