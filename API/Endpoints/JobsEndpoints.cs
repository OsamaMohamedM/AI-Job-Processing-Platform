using AiJobPlatform.Api.Filter;
using AiJobPlatform.Application.Jobs;
using Application.Common;
using Application.Jobs.Features.GetJobResult;
using Application.Jobs.Features.ListJobs;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace AiJobPlatform.Api.Endpoints;

public static class JobsEndpoints
{
    public static IEndpointRouteBuilder MapJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/jobs").RequireAuthorization();

        g.MapPost("", async (CreateJobRequest req, ICurrentUser user, CreateJobHandler h, CancellationToken ct) =>
        {
            var r = await h.HandleAsync(user.UserId, req, ct);
            return Results.Accepted($"/api/jobs/{r.JobId}", r);
        })
        .AddEndpointFilter<ValidationFilter<CreateJobRequest>>()
        .WithMetadata(new RequestSizeLimitAttribute(1_048_576));

        g.MapGet("", async ([AsParameters] ListJobsQuery q, ICurrentUser user, ListJobsHandler h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(user.UserId, q, ct)))
         .AddEndpointFilter<ValidationFilter<ListJobsQuery>>();

        g.MapGet("/{id:guid}", async (Guid id, ICurrentUser user, GetJobHandler h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(user.UserId, id, ct)));

        g.MapGet("/{id:guid}/result", async (Guid id, ICurrentUser user, GetJobResultHandler h, CancellationToken ct) =>
            Results.Ok(await h.HandleAsync(user.UserId, id, ct)));

        return app;
    }
}