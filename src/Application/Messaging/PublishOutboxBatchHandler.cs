namespace Application.Messaging;

using System.Diagnostics;
using Application.IRepositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

public sealed class PublishOutboxBatchHandler(
    IOutboxStoreRepository store, IMessagePublisher publisher, IJobRepository jobs, IUnitOfWork uow,
    IOptions<OutboxOptions> options, TimeProvider clock, ILogger<PublishOutboxBatchHandler> logger)
{
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var o = options.Value;
        var batch = await store.GetDueAsync(o.BatchSize, clock.GetUtcNow().UtcDateTime, ct);
        if (batch.Count > 0)
        {
            LogOutboxBatchLoaded(logger, batch.Count, null);
        }

        var published = 0;

        foreach (var msg in batch)
        {
            JobQueuedMessage? payload = null;
            try { payload = JsonSerializer.Deserialize<JobQueuedMessage>(msg.Payload, MessageJson.Options); }
            catch (JsonException) { }

            var startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                await publisher.PublishAsync(new OutboundMessage(msg.Id, msg.Type, msg.Payload, payload?.CorrelationId), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                var now = clock.GetUtcNow().UtcDateTime;
                var backoff = Math.Min(Math.Pow(2, Math.Min(msg.AttemptCount + 1, 20)), o.MaxPublishBackoffSeconds);
                var nextAttemptAt = now.AddSeconds(backoff);
                msg.RecordFailure("Publish to broker failed.", nextAttemptAt);
                await uow.SaveChangesAsync(ct);

                LogOutboxPublishFailed(logger, msg.Id, msg.Id, msg.AttemptCount, nextAttemptAt, ex.GetType().Name, ex);
                LogOutboxRetryScheduled(logger, msg.Id, nextAttemptAt, null);
                break;
            }

            var publishDurationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            msg.MarkPublished();
            await uow.SaveChangesAsync(ct);
            published++;

            var jobId = payload?.JobId ?? Guid.Empty;
            LogOutboxMessagePublished(logger, msg.Id, msg.Id, jobId, publishDurationMs, null);

            if (payload?.Reason == MessageReasons.Initial.ToString())
            {
                var queued = await jobs.TryMarkQueuedAsync(payload.JobId, ct);
                if (queued)
                {
                    LogJobQueued(logger, payload.JobId, "Pending", "Queued", msg.Id, null);
                }
            }
        }
        return published;
    }

    private static readonly Action<ILogger, int, Exception?> LogOutboxBatchLoaded =
        LoggerMessage.Define<int>(
            LogLevel.Debug,
            new EventId(401, "OutboxBatchLoaded"),
            "OutboxBatchLoaded {BatchSize}");

    private static readonly Action<ILogger, Guid, Guid, Guid, double, Exception?> LogOutboxMessagePublished =
        LoggerMessage.Define<Guid, Guid, Guid, double>(
            LogLevel.Information,
            new EventId(402, "OutboxMessagePublished"),
            "OutboxMessagePublished {OutboxMessageId} {MessageId} {JobId} {DurationMs}");

    private static readonly Action<ILogger, Guid, Guid, int, DateTime, string, Exception?> LogOutboxPublishFailed =
        LoggerMessage.Define<Guid, Guid, int, DateTime, string>(
            LogLevel.Warning,
            new EventId(403, "OutboxPublishFailed"),
            "OutboxPublishFailed {OutboxMessageId} {MessageId} {PublishAttempt} {NextAttemptAt} {ExceptionType}");

    private static readonly Action<ILogger, Guid, DateTime, Exception?> LogOutboxRetryScheduled =
        LoggerMessage.Define<Guid, DateTime>(
            LogLevel.Information,
            new EventId(404, "OutboxRetryScheduled"),
            "OutboxRetryScheduled {OutboxMessageId} {NextAttemptAt}");

    private static readonly Action<ILogger, Guid, string, string, Guid, Exception?> LogJobQueued =
        LoggerMessage.Define<Guid, string, string, Guid>(
            LogLevel.Information,
            new EventId(405, "JobQueued"),
            "JobQueued {JobId} {PreviousStatus} {NewStatus} {MessageId}");
}