namespace Application.Jobs.Processing;

using Application.AI;
using Application.Common;
using Application.IRepositories;
using Application.Jobs.JobTypes;
using Application.Messaging;
using Domain.Jobs.Entities;
using Domain.Jobs.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

public class ProcessJobMessageHandler(
    IProcessedMessageRepository processed, IJobRepository jobs, JobTypeRegistry registry, IAiProvider ai,
    IOutboxRepository outbox, IUnitOfWork uow, RetryPolicy retryPolicy, IOptions<ProcessingOptions> processing, TimeProvider clock,
    ILogger<ProcessJobMessageHandler> logger)
{
    private const int MaxClaimTries = 3;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private TimeSpan Lease => TimeSpan.FromSeconds(processing.Value.LeaseSeconds);

    private sealed record AiFailure(AiErrorKind Kind, TimeSpan? RetryAfter, int? StatusCode, Exception Exception);

    public async Task<MessageDisposition> HandleAsync(ConsumedMessage msg, CancellationToken ct)
    {
        logger.LogInformation(
            "Handling consumed message. MessageId={MessageId}, JobId={JobId}",
            msg.MessageId, msg.JobId);

        try
        {
            return await HandleCoreAsync(msg, ct);
        }
        catch (UniqueConstraintViolationException)
        {
            logger.LogWarning(
                "Unique constraint violation while handling message. MessageId={MessageId}, JobId={JobId}",
                msg.MessageId, msg.JobId);

            uow.ClearTracking();

            return MessageDisposition.Ack;
        }
    }

    private async Task<MessageDisposition> HandleCoreAsync(ConsumedMessage msg, CancellationToken ct)
    {
        logger.LogInformation(
            "Checking processed message. MessageId={MessageId}, JobId={JobId}",
            msg.MessageId, msg.JobId);

        var seen = await processed.FindAsync(msg.MessageId, ct);
        if (seen is not null)
        {
            logger.LogInformation(
                "Message already processed. MessageId={MessageId}, Outcome={Outcome}",
                msg.MessageId, seen.Outcome);

            return seen.Outcome == ProcessedOutcome.DeadLettered
                ? MessageDisposition.RejectToDlq
                : MessageDisposition.Ack;
        }

        logger.LogInformation(
            "Claiming job for processing. MessageId={MessageId}, JobId={JobId}",
            msg.MessageId, msg.JobId);

        var (job, early) = await ClaimAsync(msg, ct);
        if (early is { } disposition)
        {
            logger.LogInformation(
                "Job claim ended early. MessageId={MessageId}, JobId={JobId}, Disposition={Disposition}",
                msg.MessageId, msg.JobId, disposition);

            return disposition;
        }

        var sw = Stopwatch.StartNew();

        logger.LogInformation(
            "Job claimed. JobId={JobId}, Type={JobType}",
            job!.Id, job.Type);

        logger.LogInformation(
            "AI execution started. JobId={JobId}",
            job.Id);

        var (resultJson, failure) = await ExecuteAsync(job, ct);

        logger.LogInformation(
            "AI execution finished. JobId={JobId}, ElapsedMs={ElapsedMs}, Failure={Failure}",
            job.Id, sw.ElapsedMilliseconds, failure?.Kind.ToString() ?? "None");

        return failure is null
            ? await CompleteAsync(msg, job, resultJson!, sw, ct)
            : await FailAsync(msg, job, failure, ct);
    }

    private async Task<(Job? Job, MessageDisposition? Early)> ClaimAsync(ConsumedMessage msg, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxClaimTries; attempt++)
        {
            logger.LogInformation(
                "Loading job for processing. JobId={JobId}, ClaimAttempt={ClaimAttempt}",
                msg.JobId, attempt);

            var job = await jobs.GetForProcessingAsync(msg.JobId, ct);
            if (job is null)
            {
                logger.LogWarning(
                    "Job not found. JobId={JobId}",
                    msg.JobId);

                return (null, MessageDisposition.RejectToDlq);
            }

            var now = Now;
            var result = job.StartAttempt(now, Lease, "ProcessJobMessageHandler");

            logger.LogInformation(
                "Job start attempt evaluated. JobId={JobId}, Result={Result}, AttemptCount={AttemptCount}",
                job.Id, result, job.AttemptCount);

            switch (result)
            {
                case StartAttemptResult.InFlight:
                    logger.LogWarning(
                        "Job is already in flight. JobId={JobId}, LeaseExpiresAt={LeaseExpiresAt}",
                        job.Id, job.LeaseExpiresAt);

                    Defer(msg, job.LeaseExpiresAt!.Value.AddSeconds(1));
                    break;

                case StartAttemptResult.AlreadyFinished:
                    logger.LogInformation(
                        "Job already finished. JobId={JobId}",
                        job.Id);

                    processed.Add(ProcessedMessage.Create(msg.MessageId, ProcessedOutcome.Skipped, now));
                    break;

                case StartAttemptResult.AttemptsExhausted:
                    logger.LogWarning(
                        "Job attempts exhausted. JobId={JobId}",
                        job.Id);

                    processed.Add(ProcessedMessage.Create(msg.MessageId, ProcessedOutcome.DeadLettered, now));
                    break;
            }

            try
            {
                logger.LogInformation(
                    "Saving job claim. JobId={JobId}",
                    job.Id);

                await uow.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Job claim saved. JobId={JobId}",
                    job.Id);
            }
            catch (ConcurrencyConflictException)
            {
                logger.LogWarning(
                    "Concurrency conflict while claiming job. JobId={JobId}, ClaimAttempt={ClaimAttempt}",
                    job.Id, attempt);

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

        logger.LogWarning(
            "Maximum claim attempts reached. Deferring message. JobId={JobId}",
            msg.JobId);

        Defer(msg, Now + Lease);

        logger.LogInformation(
            "Saving deferred message. JobId={JobId}",
            msg.JobId);

        await uow.SaveChangesAsync(ct);

        logger.LogInformation(
            "Deferred message saved. JobId={JobId}",
            msg.JobId);

        return (null, MessageDisposition.Ack);
    }

    private async Task<(string? Json, AiFailure? Failure)> ExecuteAsync(Job job, CancellationToken ct)
    {
        try
        {
            logger.LogInformation(
                "Resolving job type handler. JobId={JobId}, Type={JobType}",
                job.Id, job.Type);

            var handler = registry.Get(job.Type);

            logger.LogInformation(
                "Building AI request. JobId={JobId}",
                job.Id);

            var request = handler.BuildRequest(job.Input);

            logger.LogInformation(
                "Calling AI provider. JobId={JobId}",
                job.Id);

            var response = await ai.CompleteAsync(request, ct);

            logger.LogInformation(
                "AI provider returned response. JobId={JobId}",
                job.Id);

            var result = handler.ParseResult(response.Text);

            logger.LogInformation(
                "AI response parsed successfully. JobId={JobId}",
                job.Id);

            return (result, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogWarning(
                "Job processing cancelled. JobId={JobId}",
                job.Id);

            throw;
        }
        catch (AiProviderException ex)
        {
            logger.LogWarning(
                ex,
                "AI provider exception. JobId={JobId}, Kind={Kind}, StatusCode={StatusCode}",
                job.Id, ex.Kind, ex.StatusCode);

            return (null, new AiFailure(ex.Kind, ex.RetryAfter, ex.StatusCode, ex));
        }
        catch (InvalidAiResponseException ex)
        {
            logger.LogWarning(
                ex,
                "Invalid AI response. JobId={JobId}",
                job.Id);

            return (null, new AiFailure(AiErrorKind.InvalidResponse, null, null, ex));
        }
        catch (UnsupportedJobTypeException ex)
        {
            logger.LogWarning(
                ex,
                "Unsupported job type. JobId={JobId}",
                job.Id);

            return (null, new AiFailure(AiErrorKind.UnsupportedJobType, null, null, ex));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error during job execution. JobId={JobId}",
                job.Id);

            return (null, new AiFailure(AiErrorKind.Unexpected, null, null, ex));
        }
    }

    private async Task<MessageDisposition> CompleteAsync(ConsumedMessage msg, Job job, string resultJson, Stopwatch sw, CancellationToken ct)
    {
        var now = Now;

        logger.LogInformation(
            "Marking job as completed. JobId={JobId}",
            job.Id);

        job.Complete(resultJson, "AI Worker");

        processed.Add(ProcessedMessage.Create(msg.MessageId, ProcessedOutcome.Completed, now));

        try
        {
            logger.LogInformation(
                "Saving completed job and processed message. JobId={JobId}, MessageId={MessageId}",
                job.Id, msg.MessageId);

            await uow.SaveChangesAsync(ct);

            logger.LogInformation(
                "Completed job saved successfully. JobId={JobId}, ElapsedMs={ElapsedMs}",
                job.Id, sw.ElapsedMilliseconds);
        }
        catch (ConcurrencyConflictException)
        {
            logger.LogWarning(
                "Concurrency conflict while completing job. JobId={JobId}",
                job.Id);

            return MessageDisposition.Ack;
        }

        return MessageDisposition.Ack;
    }

    private async Task<MessageDisposition> FailAsync(ConsumedMessage msg, Job job, AiFailure failure, CancellationToken ct)
    {
        var safe = JobFailureMessages.For(failure.Kind);
        var retryable = failure.Kind.IsRetryable();

        logger.LogWarning(
            "Handling job failure. JobId={JobId}, Kind={Kind}, Retryable={Retryable}, CanRetry={CanRetry}",
            job.Id, failure.Kind, retryable, job.CanRetry());

        if (!retryable || !job.CanRetry())
            return await DeadLetterAsync(msg, job, safe, ct);

        var now = Now;
        var delay = retryPolicy.Delay(job.AttemptCount, failure.RetryAfter);

        logger.LogInformation(
            "Scheduling job retry. JobId={JobId}, AttemptCount={AttemptCount}, DelaySeconds={DelaySeconds}",
            job.Id, job.AttemptCount, delay.TotalSeconds);

        job.ScheduleRetry(safe, "ProcessJobMessageHandler");

        outbox.Add(
            JobQueuedMessage.Create(job.Id, msg.JobType, msg.CorrelationId, MessageReasons.Retry.ToString()),
            availableAt: now + delay);

        processed.Add(ProcessedMessage.Create(msg.MessageId, ProcessedOutcome.Retried, now));

        try
        {
            logger.LogInformation(
                "Saving retry schedule. JobId={JobId}",
                job.Id);

            await uow.SaveChangesAsync(ct);

            logger.LogInformation(
                "Retry schedule saved. JobId={JobId}",
                job.Id);
        }
        catch (ConcurrencyConflictException)
        {
            logger.LogWarning(
                "Concurrency conflict while scheduling retry. JobId={JobId}",
                job.Id);

            uow.ClearTracking();
            return MessageDisposition.Ack;
        }

        return MessageDisposition.Ack;
    }

    private async Task<MessageDisposition> DeadLetterAsync(ConsumedMessage msg, Job job, string safeError, CancellationToken ct)
    {
        var now = Now;

        logger.LogWarning(
            "Dead-lettering job. JobId={JobId}",
            job.Id);

        job.DeadLetter(safeError, "AIWorker");

        processed.Add(ProcessedMessage.Create(msg.MessageId, ProcessedOutcome.DeadLettered, now));

        try
        {
            logger.LogInformation(
                "Saving dead-lettered job. JobId={JobId}",
                job.Id);

            await uow.SaveChangesAsync(ct);

            logger.LogInformation(
                "Dead-lettered job saved. JobId={JobId}",
                job.Id);
        }
        catch (ConcurrencyConflictException)
        {
            logger.LogWarning(
                "Concurrency conflict while dead-lettering job. JobId={JobId}",
                job.Id);

            uow.ClearTracking();
            return MessageDisposition.Ack;
        }

        return MessageDisposition.RejectToDlq;
    }

    private void Defer(ConsumedMessage msg, DateTime availableAt)
    {
        logger.LogInformation(
            "Deferring message. JobId={JobId}, AvailableAt={AvailableAt}",
            msg.JobId, availableAt);

        outbox.Add(
            JobQueuedMessage.Create(msg.JobId, msg.JobType, msg.CorrelationId, MessageReasons.Deferred.ToString()),
            availableAt);
    }
}