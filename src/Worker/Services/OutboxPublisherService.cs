using Application.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiJobPlatform.Worker;

public sealed class OutboxPublisherService(
    IServiceScopeFactory scopes, IOptions<OutboxOptions> options, ILogger<OutboxPublisherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(o.PollIntervalMilliseconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int published;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    published = await scope.ServiceProvider
                        .GetRequiredService<PublishOutboxBatchHandler>().HandleAsync(stoppingToken);
                } while (published >= o.BatchSize && !stoppingToken.IsCancellationRequested);

                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                LogOutboxPublisherLoopFailed(logger, ex.GetType().Name, ex);
                try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private static readonly Action<ILogger, string, Exception?> LogOutboxPublisherLoopFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(501, "OutboxPublisherLoopFailed"),
            "OutboxPublisherLoopFailed {ExceptionType}");
}