using Application.Common;
using Application.IRepositories;
using Application.Jobs;
using Application.Jobs.JobTypes;
using Application.Messaging;
using Domain.Jobs.Entities;
using Domain.Jobs.Enums;
using Microsoft.Extensions.Options;

namespace AiJobPlatform.Application.Jobs;

public class CreateJobHandler(
    JobTypeRegistry registry, IJobRepository jobs, IOutboxRepository outbox, IUnitOfWork uow,
    IOptions<RetryOptions> retry, ICorrelationContext correlation
    //,ILogger<CreateJobHandler> log
    )
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

        outbox.Add(JobQueuedMessage.Create(job.Id, type.ToString().ToLowerInvariant(),
                                           correlation.CorrelationId, MessageReasons.Initial.ToString()),
                   availableAt: DateTime.UtcNow);

        await uow.SaveChangesAsync(ct);

        //log.LogInformation("JobCreated {JobId} {JobType} {InputLength}", job.Id, type, inputJson.Length);
        return new CreateJobResponse(job.Id, job.Status.ToString());
    }
}