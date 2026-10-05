using AiJobPlatform.Application.Jobs;
using Application.Common;
using Application.Identity;
using Application.Identity.Login;
using Application.Identity.Register;
using Application.Jobs;
using Application.Jobs.Features.GetJobResult;
using Application.Jobs.Features.ListJobs;
using Application.Jobs.JobTypes;
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

        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<CreateJobHandler>();
        services.AddScoped<GetJobHandler>();
        services.AddScoped<GetJobResultHandler>();
        services.AddScoped<ListJobsHandler>();



        services.AddSingleton<IJobTypeHandler, SummarizationJobType>();
        services.AddSingleton<IJobTypeHandler, ClassificationJobType>();
        services.AddSingleton<JobTypeRegistry>();

        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<CreateJobRequest>, CreateJobRequestValidator>();
        services.AddScoped<IValidator<ListJobsQuery>, ListJobsQueryValidator>();

        return services;
    }
}