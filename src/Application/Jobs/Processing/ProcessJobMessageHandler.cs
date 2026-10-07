namespace Application.Jobs.Processing;

using Application.AI;
using Application.Common;
using Application.IRepositories;
using Application.Jobs.JobTypes;
using Application.Messaging;
using Domain.Jobs.Entities;
using Domain.Jobs.Enums;
using Microsoft.Extensions.Options;
using System.Diagnostics;

public class ProcessJobMessageHandler(
    IProcessedMessageRepository processed, IJobRepository jobs, JobTypeRegistry registry, IAiProvider ai,
    IOutboxRepository outbox, IUnitOfWork uow, IOptions<ProcessingOptions> processing, TimeProvider clock)
{
    private const int MaxClaimTries = 3;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private TimeSpan Lease => TimeSpan.FromSeconds(processing.Value.LeaseSeconds);

    private sealed record AiFailure(AiErrorKind Kind, TimeSpan? RetryAfter, int? StatusCode, Exception Exception);

    public async Task<MessageDisposition> HandleAsync(ConsumedMessage msg, CancellationToken ct)
    {
        try { return await HandleCoreAsync(msg, ct); }
        catch (UniqueConstraintViolationException)
        {
            uow.ClearTracking();

            return MessageDisposition.Ack;
        }
    }

    private async Task<MessageDisposition> HandleCoreAsync(ConsumedMessage msg, CancellationToken ct)
    {
        var seen = await processed.FindAsync(msg.MessageId, ct);
        if (seen is not null)
        {
            return seen.Outcome == ProcessedOutcome.DeadLettered ? MessageDisposition.RejectToDlq : MessageDisposition.Ack;
        }

        var (job, early) = await ClaimAsync(msg, ct);
        if (early is { } disposition) return disposition;

        var sw = Stopwatch.StartNew();
        var (resultJson, failure) = await ExecuteAsync(job, ct);
        return failure is null
            ? await CompleteAsync(msg, job, resultJson!, sw, ct)
            : await FailAsync(msg, job, failure, ct);
    }

    private async Task<(Job? Job, MessageDisposition? Early)> ClaimAsync(ConsumedMessage msg, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxClaimTries; attempt++)
        {
            var job = await jobs.GetForProcessingAsync(msg.JobId, ct);
            if (job is null)
            {
                return (null, MessageDisposition.RejectToDlq);
            }

            var now = Now;
            var result = job.StartAttempt(now, Lease, "ProcessJobMessageHandler");
            switch (result)
            {
                case StartAttemptResult.InFlight:
                    Defer(msg, job.LeaseExpiresAt!.Value.AddSeconds(1));
                    break;

                case StartAttemptResult.AlreadyFinished:
                    processed.Add(ProcessedMessage.Create(msg.MessageId, ProcessedOutcome.Skipped, now));
                    break;

                case StartAttemptResult.AttemptsExhausted:
                    processed.Add(ProcessedMessage.Create(msg.MessageId, ProcessedOutcome.DeadLettered, now));
                    break;
            }

            try { await uow.SaveChangesAsync(ct); }
            catch (ConcurrencyConflictException)
            {
                uow.ClearTracking();
                continue;
            }

            return result switch
            {
                StartAttemptResult.Started => (job, null),
                StartAttemptResult.AttemptsExhausted => (null, MessageDisposition.RejectToDlq),
                _ => (null, MessageDisposition.Ack)
            };
        }

        Defer(msg, Now + Lease);
        await uow.SaveChangesAsync(ct);
        return (null, MessageDisposition.Ack);
    }

    private async Task<(string? Json, AiFailure? Failure)> ExecuteAsync(Job job, CancellationToken ct)
    {
        try
        {
            var handler = registry.Get(job.Type);
            var response = await ai.CompleteAsync(handler.BuildRequest(job.Input), ct);
            return (handler.ParseResult(response.Text), null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }   // shutdown: no ACK
        catch (AiProviderException ex) { return (null, new AiFailure(ex.Kind, ex.RetryAfter, ex.StatusCode, ex)); }
        catch (InvalidAiResponseException ex) { return (null, new AiFailure(AiErrorKind.InvalidResponse, null, null, ex)); }
        catch (UnsupportedJobTypeException ex) { return (null, new AiFailure(AiErrorKind.UnsupportedJobType, null, null, ex)); }
        catch (Exception ex) { return (null, new AiFailure(AiErrorKind.Unexpected, null, null, ex)); }
    }

    private async Task<MessageDisposition> CompleteAsync(ConsumedMessage msg, Job job, string resultJson, Stopwatch sw, CancellationToken ct)
    {
        var now = Now;
        job.Complete(resultJson, "AI Worker");
        processed.Add(ProcessedMessage.Create(msg.MessageId, ProcessedOutcome.Completed, now));
        try { await uow.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException)
        {
            return MessageDisposition.Ack;
        }
        return MessageDisposition.Ack;
    }

    private async Task<MessageDisposition> FailAsync(ConsumedMessage msg, Job job, AiFailure failure, CancellationToken ct)
    {
        return await DeadLetterAsync(msg, job, JobFailureMessages.For(failure.Kind), ct);
    }

    private async Task<MessageDisposition> DeadLetterAsync(ConsumedMessage msg, Job job, string safeError, CancellationToken ct)
    {
        var now = Now;
        job.DeadLetter(safeError, "AIWorker");
        processed.Add(ProcessedMessage.Create(msg.MessageId, ProcessedOutcome.DeadLettered, now));
        try { await uow.SaveChangesAsync(ct); }
        catch (ConcurrencyConflictException)
        {
            uow.ClearTracking();
            return MessageDisposition.Ack;
        }
        return MessageDisposition.RejectToDlq;
    }

    private void Defer(ConsumedMessage msg, DateTime availableAt)
        => outbox.Add(JobQueuedMessage.Create(msg.JobId, msg.JobType, msg.CorrelationId, MessageReasons.Deferred.ToString()), availableAt);
}