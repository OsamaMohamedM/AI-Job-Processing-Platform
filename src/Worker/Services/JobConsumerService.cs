namespace Worker.Services;

using Application.Jobs.Processing;
using Infrastructure.Persistence.Messages;
using Infrastructure.Persistence.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;
using Worker.Parser;

public sealed partial class JobConsumerService(
    RabbitMqConnectionProvider rabbit, IOptions<RabbitMqOptions> mq, IOptions<ProcessingOptions> processing,
    IOptions<HostOptions> hostOptions, IServiceScopeFactory scopes, ILogger<JobConsumerService> logger) : BackgroundService
{
    private readonly CancellationTokenSource _processingCts = new();
    private int _inFlight;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IChannel? channel = null; string? tag = null;
            try
            {
                var n = (ushort)processing.Value.MaxConcurrentJobs;
                channel = await rabbit.CreateChannelAsync(new CreateChannelOptions(
                    publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false,
                    consumerDispatchConcurrency: n), stoppingToken);
                await RabbitMqTopology.DeclareAsync(channel, mq.Value, stoppingToken);
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: n, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                var ch = channel;
                consumer.ReceivedAsync += (_, ea) => OnReceivedAsync(ch, ea);
                tag = await channel.BasicConsumeAsync(mq.Value.Queue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
                LogConsumerStarted(logger, mq.Value.Queue, n);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                LogConsumerRestarted(logger, ex.GetType().Name, ex);
                await CloseAsync(channel);
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch (OperationCanceledException) { }
                continue;
            }

            await DrainAsync(channel, tag);
        }
    }

    private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs ea)
    {
        Interlocked.Increment(ref _inFlight);
        try
        {
            LogMessageReceived(logger, mq.Value.Queue, ea.DeliveryTag);

            if (!EnvelopeParser.TryParse(ea, out var msg, out var reason) || msg is null)
            {
                LogInvalidMessage(logger, reason, mq.Value.Queue, ea.DeliveryTag);
                await SafeAsync(() => channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false).AsTask(), "NackInvalidMessage");
                return;
            }

            using var c1 = LogContext.PushProperty("CorrelationId", msg.CorrelationId);
            using var c2 = LogContext.PushProperty("MessageId", msg.MessageId);
            using var c3 = LogContext.PushProperty("JobId", msg.JobId);
            MessageDisposition disposition;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<ProcessJobMessageHandler>();
                disposition = await handler.HandleAsync(msg, _processingCts.Token);
            }
            catch (OperationCanceledException) when (_processingCts.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogUnhandledWorkerException(logger, ex.GetType().Name, ex);
                LogMessageRequeued(logger, processing.Value.RequeueDelaySeconds * 1000.0);
                try { await Task.Delay(TimeSpan.FromSeconds(processing.Value.RequeueDelaySeconds), _processingCts.Token); }
                catch (OperationCanceledException) { return; }
                await SafeAsync(() => channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true).AsTask(), "NackRequeue");
                return;
            }

            if (disposition == MessageDisposition.Ack)
            {
                LogMessageAcked(logger, ea.DeliveryTag, disposition);
                await SafeAsync(() => channel.BasicAckAsync(ea.DeliveryTag, multiple: false).AsTask(), "AckMessage");
            }
            else
            {
                LogMessageNacked(logger, ea.DeliveryTag, false, disposition);
                await SafeAsync(() => channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false).AsTask(), "NackMessage");
            }
        }
        finally { Interlocked.Decrement(ref _inFlight); }
    }

    private async Task SafeAsync(Func<Task> action, string operation = "Settlement")
    {
        try { await action(); }
        catch (Exception ex)
        {
            LogAckNackFailed(logger, operation, ex);
        }
    }

    private async Task DrainAsync(IChannel? channel, string? tag)
    {
        var inFlight = Volatile.Read(ref _inFlight);
        LogWorkerStopping(logger, inFlight);

        if (channel is { IsOpen: true } && tag is not null)
            await SafeAsync(() => channel.BasicCancelAsync(tag), "CancelConsumer");

        var grace = TimeSpan.FromSeconds(Math.Max(1, hostOptions.Value.ShutdownTimeout.TotalSeconds - 2));
        _processingCts.CancelAfter(grace);
        var deadline = DateTime.UtcNow + grace;
        while (Volatile.Read(ref _inFlight) > 0 && DateTime.UtcNow < deadline) await Task.Delay(200);

        if (Volatile.Read(ref _inFlight) > 0)
        {
            LogWorkerDrainTimeout(logger, Volatile.Read(ref _inFlight), grace.TotalMilliseconds);
        }

        await CloseAsync(channel);
        LogConsumerStopped(logger, mq.Value.Queue);
    }

    private static async Task CloseAsync(IChannel? channel)
    {
        if (channel is null) return;
        try { await channel.CloseAsync(); } catch { }
        channel.Dispose();
    }

    [LoggerMessage(
        EventId = 701,
        Level = LogLevel.Information,
        Message = "ConsumerStarted {Queue} {MaxConcurrentJobs}")]
    private static partial void LogConsumerStarted(
        ILogger logger, string queue, int maxConcurrentJobs);

    [LoggerMessage(
        EventId = 702,
        Level = LogLevel.Warning,
        Message = "ConsumerRestarted {ExceptionType}")]
    private static partial void LogConsumerRestarted(
        ILogger logger, string exceptionType, Exception exception);

    [LoggerMessage(
        EventId = 703,
        Level = LogLevel.Information,
        Message = "ConsumerStopped {Queue}")]
    private static partial void LogConsumerStopped(
        ILogger logger, string queue);

    [LoggerMessage(
        EventId = 704,
        Level = LogLevel.Debug,
        Message = "MessageReceived {Queue} {DeliveryTag}")]
    private static partial void LogMessageReceived(
        ILogger logger, string queue, ulong deliveryTag);

    [LoggerMessage(
        EventId = 705,
        Level = LogLevel.Warning,
        Message = "InvalidMessage {FailureReason} {Queue} {DeliveryTag}")]
    private static partial void LogInvalidMessage(
        ILogger logger, string failureReason, string queue, ulong deliveryTag);

    [LoggerMessage(
        EventId = 706,
        Level = LogLevel.Information,
        Message = "MessageAcked {DeliveryTag} {Outcome}")]
    private static partial void LogMessageAcked(
        ILogger logger, ulong deliveryTag, MessageDisposition outcome);

    [LoggerMessage(
        EventId = 707,
        Level = LogLevel.Warning,
        Message = "MessageNacked {DeliveryTag} {Requeue} {Outcome}")]
    private static partial void LogMessageNacked(
        ILogger logger, ulong deliveryTag, bool requeue, MessageDisposition outcome);

    [LoggerMessage(
        EventId = 708,
        Level = LogLevel.Warning,
        Message = "MessageRequeued {RequeueDelayMs}")]
    private static partial void LogMessageRequeued(
        ILogger logger, double requeueDelayMs);

    [LoggerMessage(
        EventId = 709,
        Level = LogLevel.Error,
        Message = "UnhandledWorkerException {ExceptionType}")]
    private static partial void LogUnhandledWorkerException(
        ILogger logger, string exceptionType, Exception exception);

    [LoggerMessage(
        EventId = 710,
        Level = LogLevel.Error,
        Message = "AckNackFailed {Operation}")]
    private static partial void LogAckNackFailed(
        ILogger logger, string operation, Exception exception);

    [LoggerMessage(
        EventId = 711,
        Level = LogLevel.Information,
        Message = "WorkerStopping {InFlightCount}")]
    private static partial void LogWorkerStopping(
        ILogger logger, int inFlightCount);

    [LoggerMessage(
        EventId = 712,
        Level = LogLevel.Warning,
        Message = "WorkerDrainTimeout {InFlightCount} {DurationMs}")]
    private static partial void LogWorkerDrainTimeout(
        ILogger logger, int inFlightCount, double durationMs);
}