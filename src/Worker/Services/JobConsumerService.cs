namespace Worker.Services;

using Application.Jobs.Processing;
using Infrastructure.Persistence.Messages;
using Infrastructure.Persistence.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;
using Worker.Parser;

public sealed class JobConsumerService(
    RabbitMqConnectionProvider rabbit, IOptions<RabbitMqOptions> mq, IOptions<ProcessingOptions> processing,
    IOptions<HostOptions> hostOptions, IServiceScopeFactory scopes) : BackgroundService
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

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
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
            if (!EnvelopeParser.TryParse(ea, out var msg, out var reason) || msg is null)
            {
                await SafeAsync(() => channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false).AsTask());
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
                try { await Task.Delay(TimeSpan.FromSeconds(processing.Value.RequeueDelaySeconds), _processingCts.Token); }
                catch (OperationCanceledException) { return; }
                await SafeAsync(() => channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true).AsTask());
                return;
            }

            if (disposition == MessageDisposition.Ack)
                await SafeAsync(() => channel.BasicAckAsync(ea.DeliveryTag, multiple: false).AsTask());
            else
                await SafeAsync(() => channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false).AsTask());
        }
        finally { Interlocked.Decrement(ref _inFlight); }
    }

    private async Task SafeAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { }
    }

    private async Task DrainAsync(IChannel? channel, string? tag)
    {
        if (channel is { IsOpen: true } && tag is not null)
            await SafeAsync(() => channel.BasicCancelAsync(tag));

        var grace = TimeSpan.FromSeconds(Math.Max(1, hostOptions.Value.ShutdownTimeout.TotalSeconds - 2));
        _processingCts.CancelAfter(grace);
        var deadline = DateTime.UtcNow + grace;
        while (Volatile.Read(ref _inFlight) > 0 && DateTime.UtcNow < deadline) await Task.Delay(200);

        await CloseAsync(channel);
    }

    private static async Task CloseAsync(IChannel? channel)
    {
        if (channel is null) return;
        try { await channel.CloseAsync(); } catch { }
        channel.Dispose();
    }
}