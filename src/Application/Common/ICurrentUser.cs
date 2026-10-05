namespace Application.Common;

public interface ICurrentUser
{
    Guid UserId { get; }
}