using Application.IRepositories;
using Application.Messaging;
using Infrastructure.Persistence.Messages;
using Infrastructure.Persistence.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;

namespace AiJobPlatform.Infrastructure.Messaging;

public sealed class RabbitMqMessagePublisher(RabbitMqConnectionProvider provider, IOptions<RabbitMqOptions> options)
    : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(OutboundMessage message, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var o = options.Value;
            var ch = await GetChannelAsync(o, ct);
            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.MessageId.ToString(),
                CorrelationId = message.CorrelationId,
                Type = "job.queued"
            };
            await ch.BasicPublishAsync(o.Exchange, o.RoutingKey, mandatory: true,
                                       basicProperties: props, body: Encoding.UTF8.GetBytes(message.Body), cancellationToken: ct);
        }
        catch
        {
            await DiscardChannelAsync();
            throw;
        }
        finally { _gate.Release(); }
    }

    private async Task<IChannel> GetChannelAsync(RabbitMqOptions o, CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;
        _channel = await provider.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await RabbitMqTopology.DeclareAsync(_channel, o, ct);
        return _channel;
    }

    private async Task DiscardChannelAsync()
    {
        var c = _channel; _channel = null;
        if (c is not null) { try { await c.CloseAsync(); } catch { } c.Dispose(); }
    }

    public async ValueTask DisposeAsync() => await DiscardChannelAsync();
}