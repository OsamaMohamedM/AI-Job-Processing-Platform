using Application.Common;
using Application.IRepositories;
using Application.Jobs;
using Application.Jobs.JobTypes;
using Application.Messaging;
using Domain.Jobs.Entities;
using Domain.Jobs.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiJobPlatform.Application.Jobs;

public partial class CreateJobHandler(
    JobTypeRegistry registry, IJobRepository jobs, IOutboxRepository outbox, IUnitOfWork uow,
    IOptions<RetryOptions> retry, ICorrelationContext correlation,
    ILogger<CreateJobHandler> log)
{
    public async Task<CreateJobResponse> HandleAsync(Guid userId, CreateJobRequest request, CancellationToken ct)
    {
        if (!EnumParser.TryParse<JobType>(request.Type, out var type))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                { nameof(request.Type), new[] { $"Invalid job type '{request.Type}'." } }
            });
        }
        var errors = registry.Get(type).ValidateInput(request.Input);
        if (errors is not null) throw new ValidationException(errors);

        var inputJson = request.Input.GetRawText();

        var job = Job.Create(userId, type, inputJson, retry.Value.MaxAttempts);
        jobs.Add(job);

        var availableAt = DateTime.UtcNow;
        var queuedMessage = JobQueuedMessage.Create(job.Id, type.ToString().ToLowerInvariant(),
                                           correlation.CorrelationId, MessageReasons.Initial.ToString());
        outbox.Add(queuedMessage, availableAt: availableAt);

        await uow.SaveChangesAsync(ct);

        LogJobCreated(log, job.Id, type.ToString(), job.Status.ToString(), queuedMessage.MessageId, correlation.CorrelationId, job.MaxAttempts, userId);
        LogOutboxMessageCreated(log, queuedMessage.MessageId, queuedMessage.MessageId, job.Id, queuedMessage.Reason, availableAt, correlation.CorrelationId);

        return new CreateJobResponse(job.Id, job.Status.ToString());
    }

    [LoggerMessage(
        EventId = 301,
        Level = LogLevel.Information,
        Message = "JobCreated {JobId} {JobType} {JobStatus} {MessageId} {CorrelationId} {MaxAttempts} {UserId}")]
    private static partial void LogJobCreated(
        ILogger logger, Guid jobId, string jobType, string jobStatus, Guid messageId, string correlationId, int maxAttempts, Guid userId);

    [LoggerMessage(
        EventId = 302,
        Level = LogLevel.Information,
        Message = "OutboxMessageCreated {OutboxMessageId} {MessageId} {JobId} {MessageReason} {AvailableAt} {CorrelationId}")]
    private static partial void LogOutboxMessageCreated(
        ILogger logger, Guid outboxMessageId, Guid messageId, Guid jobId, string messageReason, DateTime availableAt, string correlationId);
}