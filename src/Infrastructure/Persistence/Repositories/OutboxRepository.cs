using Application.IRepositories;
using Application.Messaging;
using Infrastructure.Persistence.Messages;
using System.Text.Json;

namespace Infrastructure.Persistence.Repositories;

public class OutboxRepository(AppDbContext db) : IOutboxRepository
{
    public void Add(JobQueuedMessage m, DateTime availableAt)
        => db.OutboxMessages.Add(OutboxMessage.Create(
            m.MessageId, JobQueuedMessage.OutboxType,
            JsonSerializer.Serialize(m, MessageJson.Options), availableAt));
}