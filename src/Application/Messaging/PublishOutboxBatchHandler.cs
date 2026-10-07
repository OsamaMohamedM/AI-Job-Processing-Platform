namespace Application.Messaging;

using Application.IRepositories;
using Microsoft.Extensions.Options;
using System.Text.Json;

public sealed class PublishOutboxBatchHandler(
    IOutboxStoreRepository store, IMessagePublisher publisher, IJobRepository jobs, IUnitOfWork uow,
    IOptions<OutboxOptions> options, TimeProvider clock)
{
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var o = options.Value;
        var batch = await store.GetDueAsync(o.BatchSize, clock.GetUtcNow().UtcDateTime, ct);
        var published = 0;

        foreach (var msg in batch)
        {
            JobQueuedMessage? payload = null;
            try { payload = JsonSerializer.Deserialize<JobQueuedMessage>(msg.Payload, MessageJson.Options); }
            catch (JsonException) { }

            try
            {
                await publisher.PublishAsync(new OutboundMessage(msg.Id, msg.Type, msg.Payload, payload?.CorrelationId), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                var now = clock.GetUtcNow().UtcDateTime;
                var backoff = Math.Min(Math.Pow(2, Math.Min(msg.AttemptCount + 1, 20)), o.MaxPublishBackoffSeconds);
                msg.RecordFailure("Publish to broker failed.", now.AddSeconds(backoff));
                await uow.SaveChangesAsync(ct);

                break;
            }

            msg.MarkPublished();
            await uow.SaveChangesAsync(ct);
            published++;

            if (payload?.Reason == MessageReasons.Initial.ToString())
                await jobs.TryMarkQueuedAsync(payload.JobId, ct);
        }
        return published;
    }
}