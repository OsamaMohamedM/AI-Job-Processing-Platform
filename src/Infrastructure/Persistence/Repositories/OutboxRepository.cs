using Application.IRepositories;
using Application.Messaging;
using System.Text.Json;

namespace Infrastructure.Persistence.Repositories;

public class OutboxRepository(AppDbContext db) : IOutboxRepository
{
    public void Add(JobQueuedMessage message, DateTime availableAt)
        => db.OutboxMessages.Add(OutboxMessage.Create(
            message.MessageId, JobQueuedMessage.OutboxType,
            JsonSerializer.Serialize(message, MessageJson.Options), availableAt));
}