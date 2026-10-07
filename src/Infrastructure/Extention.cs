using AiJobPlatform.Infrastructure.Messaging;
using Application.AI;
using Application.IRepositories;
using Application.Messaging;
using Infrastructure.AI;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Messages;
using Infrastructure.Persistence.Messaging;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Default")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DefaultConnection:ConnectionString"];

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            }));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<Application.IRepositories.IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IJobQueriesRepository, JobQueries>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IOutboxStoreRepository, OutboxStoreRepository>();
        services.AddScoped<IProcessedMessageRepository, ProcessedMessageStoreRepository>();

        return services;
    }

    public static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<RabbitMqOptions>().Bind(config.GetSection("RabbitMq")).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<OutboxOptions>().Bind(config.GetSection("Outbox")).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
        return services;
    }

    public static IServiceCollection AddAiProvider(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<AiOptions>().Bind(config.GetSection("Ai")).ValidateDataAnnotations().ValidateOnStart();
        services.AddHttpClient<IAiProvider, GeminiProvider>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        return services;
    }
}