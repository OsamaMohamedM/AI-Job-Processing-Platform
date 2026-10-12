using AiJobPlatform.Application.Jobs;
using Application.Identity;
using Application.Identity.Login;
using Application.Identity.Register;
using Application.Jobs;
using Application.Jobs.Features.GetJobResult;
using Application.Jobs.Features.ListJobs;
using Application.Jobs.JobTypes;
using Application.Jobs.Processing;
using Application.Messaging;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.Configure<JobsOptions>(config.GetSection("Jobs"));
        services.Configure<RetryOptions>(config.GetSection("Retry"));

        services.AddSingleton<IJobTypeHandler, SummarizationJobType>();
        services.AddSingleton<IJobTypeHandler, ClassificationJobType>();
        services.AddSingleton<JobTypeRegistry>();
        services.AddSingleton<RetryPolicy>();
        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<CreateJobRequest>, CreateJobRequestValidator>();
        services.AddScoped<IValidator<ListJobsQuery>, ListJobsQueryValidator>();

        return services;
    }

    public static IServiceCollection AddApiUseCases(this IServiceCollection services)
    {
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<CreateJobHandler>();
        services.AddScoped<GetJobHandler>();
        services.AddScoped<GetJobResultHandler>();
        services.AddScoped<ListJobsHandler>();
        services.AddSingleton<RetryPolicy>();
        return services;
    }

    public static IServiceCollection AddWorkerUseCases(this IServiceCollection services)
    {
        services.AddScoped<ProcessJobMessageHandler>();
        services.AddScoped<PublishOutboxBatchHandler>();
        return services;
    }
}