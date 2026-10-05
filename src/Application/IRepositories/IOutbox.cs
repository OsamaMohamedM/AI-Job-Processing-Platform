using Application.Messaging;

namespace Application.IRepositories;

public interface IOutboxRepository
{
    void Add(JobQueuedMessage message, DateTime availableAt);
}